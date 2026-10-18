using Companion.Data.Extensions;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()

    // On first boot the migration check probes __EFMigrationsHistory before that table
    // exists. Npgsql reports the failed SELECT on this category at Error, so a perfectly
    // healthy first start would log a scary ERR line. Genuine database failures still
    // surface: they are reported under the Npgsql / Microsoft.EntityFrameworkCore
    // categories, not this one.
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("Starting Companion.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    // Persistence. AddCompanionPostgres owns the connection string, the DbContext, its
    // factory and the Postgres health check, and fails fast with an actionable message
    // if the connection string is missing. It deliberately does not register Identity
    // or run migrations.
    builder.Services.AddCompanionPostgres(builder.Configuration);

    // Identity is a gateway concern. Workers share the schema without it.
    builder.Services.AddCompanionIdentity();

    var app = builder.Build();

    // Companion.Api is the gateway and the only service that owns schema migrations.
    // Workers only verify the schema (EnsureCompanionMigrationsAppliedAsync), which is
    // deliberately not wired anywhere yet.
    await app.ApplyCompanionMigrationsAsync();

    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Companion.Api v1"));

    // Stub health endpoint. Migration-gated health (checking database
    // connectivity and pending migrations) is a later phase.
    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
