using Companion.Configuration;
using System.Security.Claims;
using Companion.Api.Dtos;
using Companion.Api.Features.Auth;
using Companion.Api.Features.Expenses;
using Companion.Api.Features.Images;
using Companion.Api.Options;
using Companion.Api.Services;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Companion.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()

    // Keep SQL command logging at Warning while preserving database errors.
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("Starting Companion.Api");

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddCompanionAppSettings(builder.Environment);

    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    // Persistence. AddCompanionPostgres owns the connection string, the DbContext, its
    // factory and the Postgres health check, and fails fast with an actionable message
    // if the connection string is missing. It deliberately does not register Identity
    // or run migrations.
    builder.Services.AddCompanionPostgres(builder.Configuration);

    // Identity is a gateway concern. Workers share the schema without it.
    builder.Services.AddCompanionIdentity();

    // Auth options (binds + validates JWT signing key, registers AuthOptions singleton).
    builder.Services.AddAuthOptions(builder.Configuration);

    // Authentication services: OtpService, AuthenticationService, JWT Bearer, CORS, ProblemDetails.
    builder.Services.AddAuthenticationServices(builder.Configuration);

    // Shared RabbitMQ integration, used by the transactional outbox publisher below.
    builder.Services.AddCompanionMessaging(builder.Configuration);
    builder.Services.AddHostedService<OutboxPublisher>();

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("images:read", policy => policy.RequireClaim(Role.PermissionClaimType, "platform:*"));
        options.AddPolicy("images:write", policy => policy.RequireClaim(Role.PermissionClaimType, "platform:*"));
    });
    builder.Services.AddAuthRateLimiting();

    var app = builder.Build();

    // Companion.Api is the gateway and the only service that owns schema migrations.
    // Workers only verify the schema (EnsureCompanionMigrationsAppliedAsync), which is
    // deliberately not wired anywhere yet.
    await app.ApplyCompanionMigrationsAsync();

    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Companion.Api v1"));

    app.UseExceptionHandler();
    app.UseCors("companion-web");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Report database outages through the registered PostgreSQL check.
    app.MapHealthChecks("/health");

    app.MapAuthEndpoints();
    app.MapExpenseCaptureEndpoints();
    app.MapExpenseExtractionEndpoints();
    app.MapImageEndpoints(builder.Configuration);

    app.MapGet("/api/profile",
        async (Guid profilePublicId, ClaimsPrincipal user, IAuthenticationService auth, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var subject) || subject != profilePublicId)
                return Results.Forbid();
            var result = await auth.GetProfileAsync(profilePublicId, ct);
            return result?.Found is true
                ? Results.Ok(new { result.DisplayName, result.PublicId })
                : Results.Problem(statusCode: 404, title: "Profile not found");
        })
        .WithName("GetProfile")
        .RequireAuthorization();

    app.MapPut("/api/profile",
        async (Guid profilePublicId, ProfileUpdateRequest request, ClaimsPrincipal user, IAuthenticationService auth, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var subject) || subject != profilePublicId)
                return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 255)
                return Results.Problem(statusCode: 400, title: "Invalid display name");
            var result = await auth.UpdateProfileAsync(profilePublicId, request, ct);
            return result.Found
                ? Results.Ok(new { result.DisplayName, result.PublicId })
                : Results.Problem(statusCode: 404, title: "Profile not found");
        })
        .WithName("UpdateProfile")
        .RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Companion.Api terminated unexpectedly");

    // Without this the process would exit 0, so a supervisor or orchestrator
    // would treat a failed migration (or a missing connection string) as a
    // clean shutdown and might never restart the gateway.
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory<Program> in Tests/Companion.Api.Tests.
public partial class Program { }
