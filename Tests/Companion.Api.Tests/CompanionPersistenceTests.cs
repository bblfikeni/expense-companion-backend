using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class CompanionPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public CompanionPersistenceTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PublicId_IsGenerated_ByTheDatabase()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);
        await host.ApplyCompanionMigrationsAsync();

        Guid stored;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var profile = new Companion.Data.Models.Profile
            {
                UserName = "public-id-probe",
                Email = "public-id-probe@example.com",
                FullName = "Public Id Probe",
                PopiaConsentGiven = true,
                PopiaConsentDate = DateTimeOffset.UtcNow,
            };

            // No PublicId assigned: the database default must supply it.
            Assert.Equal(Guid.Empty, profile.PublicId);
            db.Users.Add(profile);
            await db.SaveChangesAsync();
            stored = profile.PublicId;
        }

        Assert.NotEqual(Guid.Empty, stored);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var reloaded = await db.Users.SingleAsync(p => p.UserName == "public-id-probe");
            Assert.Equal(stored, reloaded.PublicId);
        }
    }

    [Fact]
    public async Task NormalizedEmail_IsUnique()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);
        await host.ApplyCompanionMigrationsAsync();

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Profile>>();
            var first = new Profile
            {
                UserName = "unique-email-a",
                Email = "duplicate@example.com",
                FullName = "First Holder",
                PopiaConsentGiven = true,
                PopiaConsentDate = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(first, "Str0ng-pass!")).Succeeded);
            Assert.NotEqual(Guid.Empty, first.PublicId);
        }

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Profile>>();
            var second = new Profile
            {
                UserName = "unique-email-b",
                Email = "DUPLICATE@example.com",
                FullName = "Second Holder",
                PopiaConsentGiven = true,
                PopiaConsentDate = DateTimeOffset.UtcNow,
            };

            // Same address in different casing: normalization must collide.
            var result = await users.CreateAsync(second, "Str0ng-pass!");
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "DuplicateEmail");
        }
    }

    [Fact]
    public async Task Schema_HasNoCheckConstraints()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);
        await host.ApplyCompanionMigrationsAsync();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_constraint WHERE contype = 'c'";
        var checkCount = (long)(await command.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(0, checkCount);
    }

    [Fact]
    public async Task Seed_Contains_PlatformAdminRole_WithPlatformPermissionClaim_AndNoUsers()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();
        using var host = CompanionHosts.Build(connectionString, withIdentity: true);
        await host.ApplyCompanionMigrationsAsync();

        using var scope = host.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();

        var admin = await roles.FindByNameAsync(Role.PlatformAdmin);
        Assert.NotNull(admin);

        var claims = await db.RoleClaims.Where(c => c.RoleId == admin.Id).ToListAsync();
        var permission = Assert.Single(claims);
        Assert.Equal(Role.PermissionClaimType, permission.ClaimType);
        Assert.Equal(Role.PlatformPermission, permission.ClaimValue);

        Assert.Equal(0, await db.Users.CountAsync());
    }
}
