using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Companion.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real gateway against a throwaway Testcontainers database instead of the
/// developer's local docker-compose instance, so the smoke test is hermetic. Program.cs
/// applies migrations on startup, so the schema is ready before the client is created.
/// </summary>
public sealed class CompanionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CompanionApiFactory(string connectionString)
        => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:CompanionDb", _connectionString);
}
