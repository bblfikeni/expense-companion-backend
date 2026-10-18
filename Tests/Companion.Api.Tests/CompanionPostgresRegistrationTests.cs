using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public class CompanionPostgresRegistrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public CompanionPostgresRegistrationTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void AddCompanionPostgres_FailsFast_WithActionableMessage_WhenConnectionStringMissing()
    {
        var builder = Host.CreateApplicationBuilder();

        var exception = Assert.Throws<InvalidOperationException>(
            () => builder.Services.AddCompanionPostgres(builder.Configuration));

        Assert.Contains("CompanionDb", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:CompanionDb", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__CompanionDb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddCompanionPostgres_Registers_ScopedContext_And_Factory()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString);

        // The scoped context must be a fresh instance per scope, not a shared singleton.
        // Both must actually be configured against the database: a missing options
        // action on AddDbContext surfaces here as "No database provider has been
        // configured". Scopes stay alive while their context is used.
        using (var scope = host.Services.CreateScope())
        {
            var first = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            Assert.True(await first.Database.CanConnectAsync());
            Assert.Contains(first.Model.GetEntityTypes(), entity => entity.GetTableName() == "profiles");
        }

        CompanionDbContext second;
        using (var scope = host.Services.CreateScope())
        {
            second = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            Assert.Contains(second.Model.GetEntityTypes(), entity => entity.GetTableName() == "profiles");
        }

        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();

        await using var fromFactory = factory.CreateDbContext();

        Assert.NotSame(second, fromFactory);

        Assert.Contains(fromFactory.Model.GetEntityTypes(), entity => entity.GetTableName() == "profiles");
        Assert.True(await fromFactory.Database.CanConnectAsync());
    }

    [Fact]
    public async Task AddCompanionPostgres_HonoursConfigureCallback_AndDefaultsSensitiveLoggingOff()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();

        using var host = CompanionHosts.Build(
            connectionString,
            configure: options =>
            {
                options.EnableRetryOnFailure = false;
                options.EnableSensitiveDataLogging = true;
            });

        var configured = host.Services.GetRequiredService<CompanionPostgresOptions>();
        Assert.False(configured.EnableRetryOnFailure);
        Assert.True(configured.EnableSensitiveDataLogging);

        // Sensitive logging off by default when nothing overrides it.
        using var defaultHost = CompanionHosts.Build(connectionString);
        Assert.False(defaultHost.Services.GetRequiredService<CompanionPostgresOptions>().EnableSensitiveDataLogging);
    }

    [Fact]
    public async Task AddCompanionPostgres_DoesNot_Register_Identity()
    {
        // Mirrors a worker: persistence only, no gateway concerns.
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: false);

        Assert.NotNull(host.Services.GetService<CompanionDbContext>());
        Assert.NotNull(host.Services.GetService<IDbContextFactory<CompanionDbContext>>());

        Assert.Null(host.Services.GetService<UserManager<Profile>>());
        Assert.Null(host.Services.GetService<RoleManager<Role>>());
        Assert.Null(host.Services.GetService<IUserStore<Profile>>());
        Assert.Null(host.Services.GetService<IRoleStore<Role>>());
        Assert.Null(host.Services.GetService<IUserClaimsPrincipalFactory<Profile>>());
    }

    [Fact]
    public async Task AddCompanionIdentity_Registers_GatewayServices()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);

        Assert.NotNull(host.Services.GetService<UserManager<Profile>>());
        Assert.NotNull(host.Services.GetService<RoleManager<Role>>());
        Assert.NotNull(host.Services.GetService<IUserStore<Profile>>());
        Assert.NotNull(host.Services.GetService<IRoleStore<Role>>());

        // The role-aware variant: it carries RoleManager<Role> so role claims
        // (the permission codes) reach the principal. The non-role variant would
        // silently drop every permission claim and RBAC would look wired while
        // authorising nothing.
        var claimsFactory = host.Services.GetRequiredService<IUserClaimsPrincipalFactory<Profile>>();
        Assert.IsType<UserClaimsPrincipalFactory<Profile, Role>>(claimsFactory);
    }

    [Fact]
    public async Task AddCompanionPostgres_Registers_PostgresHealthCheck()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString);

        var registrations = host.Services.GetRequiredService<HealthCheckServiceOptions>();
        var postgres = Assert.Single(
            registrations.Registrations,
            registration => registration.Name == CompanionPostgresServiceCollectionExtensions.PostgresHealthCheckName);
        Assert.NotNull(postgres.Factory);
    }

    [Fact]
    public async Task EnsureCompanionMigrationsAppliedAsync_FailsStartup_WhilePending_ThenPassesOnceApplied()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);

        var pendingFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.EnsureCompanionMigrationsAppliedAsync(
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(10)));

        Assert.Contains("missing", pendingFailure.Message, StringComparison.OrdinalIgnoreCase);

        // Once the gateway has migrated, the same check passes without applying anything.
        await host.ApplyCompanionMigrationsAsync();

        await host.EnsureCompanionMigrationsAppliedAsync(
            maxAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(10));
    }
}
