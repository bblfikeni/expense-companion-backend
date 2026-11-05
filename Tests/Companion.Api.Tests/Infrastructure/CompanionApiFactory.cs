using System.Security.Cryptography;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Companion.Api.Services;

namespace Companion.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real gateway against a throwaway Testcontainers database instead of the
/// developer's local docker-compose instance, so the smoke test is hermetic. Program.cs
/// applies migrations on startup, so the schema is ready before the client is created.
/// </summary>
public sealed class CompanionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly bool _disableOutboxPublisher;

    public CompanionApiFactory(
        string connectionString,
        Action<IServiceCollection>? configureServices = null,
        bool disableOutboxPublisher = true)
    {
        _connectionString = connectionString;
        _configureServices = configureServices;
        _disableOutboxPublisher = disableOutboxPublisher;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CompanionDb", _connectionString);

        // UseSetting is available while Program registers services. Configuration
        // callbacks run at Build(), after the gateway's fail-fast secret validation.
        builder.UseSetting("Auth:Jwt:Issuer", "companion-api");
        builder.UseSetting("Auth:Jwt:Audience", "companion-web");
        builder.UseSetting("Auth:Google:ClientId", "companion-test-client");
        builder.UseSetting("Auth:Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Auth:Otps:Pepper", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Auth:Otps:ExpiresInMinutes", "10");
        builder.UseSetting("Auth:Otps:MaxAttempts", "5");
        builder.UseSetting("Auth:Otps:ResendCooldownInSeconds", "60");
        builder.UseSetting("RabbitMq:ConnectionString", "host=localhost;virtualHost=/;username=test;password=test");
        builder.UseSetting("Cors:AllowedOrigin", "http://localhost:5173");
        builder.ConfigureTestServices(services =>
        {
            if (_disableOutboxPublisher)
            {
                var outboxPublisher = services.FirstOrDefault(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(OutboxPublisher));
                if (outboxPublisher is not null)
                    services.Remove(outboxPublisher);
            }

            _configureServices?.Invoke(services);
        });
    }
}
