using System.Security.Claims;
using Companion.Data;
using Microsoft.EntityFrameworkCore;

namespace Companion.Api.Features.Expenses;

public static class ExpenseExtractionEndpoints
{
    public static void MapExpenseExtractionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/expenses/{expenseId:guid}/extraction", async (
            Guid expenseId, ClaimsPrincipal user, CompanionDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
                return Results.Unauthorized();

            var expense = await (
                from item in db.Expenses.AsNoTracking()
                join membership in db.WorkspaceMemberships.AsNoTracking() on item.WorkspaceId equals membership.WorkspaceId
                join profile in db.Users.AsNoTracking() on membership.ProfileId equals profile.Id
                where item.PublicId == expenseId && profile.PublicId == profileId
                select new { item.Id, item.PublicId, item.Status })
                .SingleOrDefaultAsync(ct);
            if (expense is null)
                return Results.Problem(statusCode: 404, title: "Expense not found");

            var result = await db.ExpenseExtractions.AsNoTracking()
                .Where(extraction => extraction.ExpenseId == expense.Id)
                .OrderByDescending(extraction => extraction.CompletedAt)
                .ThenByDescending(extraction => extraction.Id)
                .Select(extraction => new ExpenseExtractionResponse(
                    expense.PublicId,
                    expense.Status,
                    extraction.Status,
                    extraction.Vendor,
                    extraction.ExpenseDate,
                    extraction.TotalAmountCents,
                    extraction.VatAmountCents,
                    extraction.VatNumber,
                    extraction.VatClassification,
                    extraction.IsValidTaxInvoice,
                    extraction.VendorConfidence,
                    extraction.DateConfidence,
                    extraction.TotalConfidence,
                    extraction.VatAmountConfidence,
                    extraction.VatNumberConfidence,
                    extraction.DocumentValidityConfidence,
                    extraction.ReviewRequired,
                    extraction.CompletedAt))
                .FirstOrDefaultAsync(ct);

            return Results.Ok(result ?? new ExpenseExtractionResponse(
                expense.PublicId, expense.Status, "Pending", null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null));
        })
        .WithName("GetExpenseExtraction")
        .RequireAuthorization("images:read")
        .Produces<ExpenseExtractionResponse>()
        .Produces(401).Produces(403).ProducesProblem(404);
    }
}