using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Microsoft.AspNetCore.Identity;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Companion.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    var connectionString = builder.Configuration.GetConnectionString("CompanionDb")
        ?? throw new InvalidOperationException(
            "Missing connection string 'CompanionDb'. Set it in appsettings.json or override " +
            "it with the ConnectionStrings__CompanionDb environment variable.");

    builder.Services.AddDbContext<CompanionDbContext>(options => options.UseCompanionDatabase(connectionString));

    // Identity storage and UserManager/RoleManager only — no authentication scheme is
    // registered here. That belongs to the auth phase, not to this persistence setup.
    builder.Services
        .AddIdentityCore<Profile>(options => options.User.RequireUniqueEmail = true)
        .AddRoles<Role>()
        .AddEntityFrameworkStores<CompanionDbContext>();

    var app = builder.Build();

    // Companion.Api is the gateway and the only service that owns schema migrations.
    // Workers only check for pending migrations (see DbContextMigrationExtensions).
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        await dbContext.ApplyMigrationsAsync(app.Logger);
    }

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
