using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Companion.Data;
using Companion.Data.Models;
using Companion.Events;
using Microsoft.EntityFrameworkCore;

namespace Companion.Api.Features.Expenses;

public static class ExpenseCaptureEndpoints
{
    public static void MapExpenseCaptureEndpoints(this WebApplication app)
    {
        app.MapPost("/api/expenses", async (
            InitializeExpenseCaptureRequest request, ClaimsPrincipal user, CompanionDbContext db, CancellationToken ct) =>
                await Initialize(request, user, db, ct))
            .WithName("InitializeExpenseCapture")
            .RequireAuthorization("images:write")
            .Produces<ExpenseCaptureResponse>(201)
            .Produces<ExpenseCaptureResponse>(200)
            .ProducesProblem(400)
            .Produces(401).Produces(403).ProducesProblem(409);

        app.MapPost("/api/expenses/{expenseId:guid}/submit", async (
            Guid expenseId, HttpContext context, CompanionDbContext db, TimeProvider clock, CancellationToken ct) =>
                await Submit(expenseId, context, db, clock, ct))
            .WithName("SubmitExpenseCapture")
            .RequireAuthorization("images:write")
            .Produces<ExpenseCaptureResponse>(202)
            .Produces(401).Produces(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> Initialize(
        InitializeExpenseCaptureRequest request, ClaimsPrincipal user, CompanionDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
            return Results.Unauthorized();
        if (request.RequestId == Guid.Empty)
            return Results.Problem(statusCode: 400, title: "A capture request ID is required.");

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({request.RequestId.ToString()}, 0))", ct);
            var workspaceIds = await db.WorkspaceMemberships.FromSqlInterpolated($"""
                SELECT m.* FROM workspace_memberships m
                JOIN profiles p ON p.id = m.profile_id
                WHERE p.public_id = {profileId}
                FOR SHARE OF m
                """)
                .Select(m => m.WorkspaceId)
                .Take(2)
                .ToListAsync(ct);
            if (workspaceIds.Count != 1)
            {
                await transaction.RollbackAsync(ct);
                return Results.Problem(statusCode: 409, title: "A single workspace membership is required to start a receipt.");
            }

            var existing = await db.Expenses.AsNoTracking()
                .SingleOrDefaultAsync(e => e.CaptureRequestId == request.RequestId, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing.WorkspaceId == workspaceIds[0]
                    ? Results.Ok(new ExpenseCaptureResponse(existing.PublicId, existing.Status))
                    : Results.Problem(statusCode: 409, title: "This capture request ID is unavailable.");
            }

            var expense = new Expense
            {
                WorkspaceId = workspaceIds[0],
                Status = "None",
                CaptureRequestId = request.RequestId,
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/expenses/{expense.PublicId}", new ExpenseCaptureResponse(expense.PublicId, expense.Status));
        });
    }

    private static async Task<IResult> Submit(
        Guid expenseId, HttpContext context, CompanionDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
            return Results.Unauthorized();

        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var expense = await db.Expenses.FromSqlInterpolated($"""
                SELECT e.* FROM expenses e
                JOIN workspace_memberships m ON m.workspace_id = e.workspace_id
                JOIN profiles p ON p.id = m.profile_id
                WHERE e.public_id = {expenseId} AND p.public_id = {profileId}
                FOR UPDATE OF e
                FOR SHARE OF m
                """).SingleOrDefaultAsync(ct);
            if (expense is null)
            {
                await transaction.RollbackAsync(ct);
                return SubmissionResult.NotFound;
            }
            if (expense.Status == "Processing")
            {
                await transaction.CommitAsync(ct);
                return SubmissionResult.AlreadySubmitted;
            }
            if (expense.Status is not ("None" or "Uploaded"))
            {
                await transaction.RollbackAsync(ct);
                return SubmissionResult.InvalidStatus;
            }
            if (!await db.ExpenseImages.AnyAsync(i => i.ExpenseId == expense.Id, ct))
            {
                await transaction.RollbackAsync(ct);
                return SubmissionResult.Empty;
            }

            var occurredAt = clock.GetUtcNow();
            var eventId = Guid.NewGuid();
            var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var message = new ReceiptUploadedEvent
            {
                ExpensePublicId = expense.PublicId,
                CorrelationId = correlationId,
                RequestedAt = occurredAt,
                EventId = eventId,
                OccurredAtUtc = occurredAt,
            };
            expense.Status = "Processing";
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = nameof(ReceiptUploadedEvent),
                Payload = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                CorrelationId = correlationId,
                CreatedAt = occurredAt,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return SubmissionResult.Queued;
        });

        return result switch
        {
            SubmissionResult.NotFound => Results.Problem(statusCode: 404, title: "Expense not found"),
            SubmissionResult.Empty => Results.Problem(statusCode: 409, title: "Add at least one image before submitting."),
            SubmissionResult.InvalidStatus => Results.Problem(statusCode: 409, title: "This expense cannot be submitted."),
            _ => Results.Accepted($"/api/expenses/{expenseId}", new ExpenseCaptureResponse(expenseId, "Processing")),
        };
    }

    private enum SubmissionResult { Queued, AlreadySubmitted, NotFound, Empty, InvalidStatus }
}