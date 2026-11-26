using System.Collections.Concurrent;
using System.Data.Common;
using Companion.Api.Tests.Infrastructure;
using Companion.Core.Interfaces;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Companion.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ImageStoreTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_FreshAndUpgrade_PreservesExistingProfilesAndStorage(bool upgrade)
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        if (upgrade)
        {
            await db.GetService<IMigrator>().MigrateAsync("20261126142810_AddExtractionConstraints");
            db.Users.Add(new Profile { UserName = "existing", FullName = "Existing", PopiaConsentDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await db.Database.MigrateAsync();
        Assert.Equal(upgrade ? 1 : 0, await db.Users.CountAsync());
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT attstorage FROM pg_attribute WHERE attrelid = 'expense_image_blobs'::regclass AND attname = 'data' AND atttypid = 'bytea'::regtype";
        Assert.Equal("e", (await command.ExecuteScalarAsync())?.ToString());
        command.CommandText = "SELECT condeferrable AND condeferred FROM pg_constraint WHERE conname = 'uq_expense_images_expense_page'";
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Store_EnforcesTenancyIdempotencyOrderingAndStatus()
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var profile = new Profile { UserName = "owner", FullName = "Owner", PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { Name = "workspace" };
        var expense = new Expense { Workspace = workspace };
        db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
        await db.SaveChangesAsync();
        var store = new PostgresImageStore(factory);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [1], 10, 20, "Receipt")));
        Assert.Single(concurrent, r => r.Status == ImageStoreStatus.Success);
        Assert.Equal(4, concurrent.Count(r => r.Status == ImageStoreStatus.AlreadyExists));
        var first = concurrent[0].Value!;
        Assert.All(concurrent, r => Assert.Equal(first.ImageId, r.Value!.ImageId));
        var second = (await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [2], 10, 20, "ProofOfPayment")).Value!;
        Assert.Equal(ImageStoreStatus.NotFound, (await store.ListAsync(Guid.NewGuid(), expense.PublicId)).Status);
        Assert.Equal(ImageStoreStatus.NotFound, (await store.GetAsync(Guid.NewGuid(), expense.PublicId, first.ImageId)).Status);
        Assert.Equal(ImageStoreStatus.NotFound, await store.DeleteAsync(Guid.NewGuid(), expense.PublicId, first.ImageId));
        Assert.Equal("Receipt", (await store.UpdateAsync(profile.PublicId, expense.PublicId, first.ImageId, false, null, 2)).Value!.Label);
        Assert.Null((await store.UpdateAsync(profile.PublicId, expense.PublicId, first.ImageId, true, null)).Value!.Label);
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(profile.PublicId, expense.PublicId, first.ImageId, true, "receipt"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [3], 1, 1, "other"));
        factory.Commands.Clear();
        var list = await store.ListAsync(profile.PublicId, expense.PublicId);
        Assert.Equal(new[] { second.ImageId, first.ImageId }, list.Value!.Select(i => i.ImageId));
        Assert.DoesNotContain(factory.Commands, sql => sql.Contains("expense_image_blobs", StringComparison.OrdinalIgnoreCase));
        factory.Commands.Clear();
        var content = await store.GetAsync(profile.PublicId, expense.PublicId, first.ImageId);
        Assert.Equal(new byte[] { 1 }, content.Value!.Content);
        Assert.Equal(2, factory.Commands.Count);
        Assert.DoesNotContain("expense_image_blobs", factory.Commands.First(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT e.data", factory.Commands.Last(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ImageStoreStatus.Success, await store.DeleteAsync(profile.PublicId, expense.PublicId, second.ImageId));
        Assert.Equal(1, Assert.Single((await store.ListAsync(profile.PublicId, expense.PublicId)).Value!).PageNo);
        expense.Status = "Processing";
        await db.SaveChangesAsync();
        Assert.Equal(ImageStoreStatus.InvalidStatus, (await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [4], 1, 1)).Status);
        Assert.Equal(ImageStoreStatus.InvalidStatus, await store.DeleteAsync(profile.PublicId, expense.PublicId, first.ImageId));
        Assert.Equal(ImageStoreStatus.InvalidStatus, (await store.UpdateAsync(profile.PublicId, expense.PublicId, first.ImageId, true, null)).Status);
    }

    [Fact]
    public async Task Database_RejectsInvalidMetadataAndBlob()
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var profile = new Profile { UserName = "owner", FullName = "Owner", PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { Name = "workspace" };
        var expense = new Expense { Workspace = workspace };
        db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
        await db.SaveChangesAsync();
        var store = new PostgresImageStore(factory);
        await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [1], 1, 1);
        foreach (var sql in new[] { "UPDATE expense_images SET label = 'invalid'", "UPDATE expense_images SET page_no = 11", "UPDATE expense_images SET width = 0", "UPDATE expense_images SET size_bytes = 5242881", "UPDATE expense_images SET sha256 = 'bad'", "UPDATE expense_image_blobs SET data = ''::bytea", "UPDATE expense_image_blobs SET data = decode(repeat('00', 5242881), 'hex')" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        await db.Database.ExecuteSqlRawAsync("UPDATE expense_image_blobs SET data = decode(repeat('00', 5242880), 'hex')");
        await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [2], 1, 1);
        foreach (var sql in new[] { "UPDATE expense_images SET public_id = (SELECT public_id FROM expense_images ORDER BY id LIMIT 1) WHERE id = (SELECT max(id) FROM expense_images)", "UPDATE expense_images SET sha256 = (SELECT sha256 FROM expense_images ORDER BY id LIMIT 1) WHERE id = (SELECT max(id) FROM expense_images)", "UPDATE expense_images SET page_no = (SELECT page_no FROM expense_images ORDER BY id LIMIT 1) WHERE id = (SELECT max(id) FROM expense_images)" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        }
    }

    [Fact]
    public async Task ConcurrentDistinctUploads_AreLimitedToTen()
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var profile = new Profile { UserName = "owner", FullName = "Owner", PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { Name = "workspace" };
        var expense = new Expense { Workspace = workspace };
        db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
        await db.SaveChangesAsync();
        var store = new PostgresImageStore(factory);
        var results = await Task.WhenAll(Enumerable.Range(1, 12).Select(i => store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [(byte)i], 1, 1)));
        Assert.Equal(10, results.Count(r => r.Status == ImageStoreStatus.Success));
        Assert.Equal(2, results.Count(r => r.Status == ImageStoreStatus.LimitReached));
        var images = (await store.ListAsync(profile.PublicId, expense.PublicId)).Value!;
        Assert.Equal(10, images.Count);
        Assert.Equal(Enumerable.Range(1, 10), images.Select(i => i.PageNo));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task RevocationInProgress_PreventsWriteAfterCommit(string operation)
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var profile = new Profile { UserName = "owner", FullName = "Owner", PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { Name = "workspace" };
        var expense = new Expense { Workspace = workspace };
        db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
        await db.SaveChangesAsync();
        var store = new PostgresImageStore(factory);
        var image = (await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [1], 1, 1)).Value!;

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM workspace_memberships WHERE workspace_id = {workspace.Id} AND profile_id = {profile.Id}");
        factory.LockExecuting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var write = Write(store, operation, profile.PublicId, expense.PublicId, image.ImageId);
        await factory.LockExecuting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var completedBeforeCommit = await Task.WhenAny(write, Task.Delay(500)) == write;
        await transaction.CommitAsync();
        var result = await write.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(completedBeforeCommit);
        Assert.Equal(ImageStoreStatus.NotFound, result);
        await using var verify = factory.CreateDbContext();
        var images = await verify.ExpenseImages.AsNoTracking().ToListAsync();
        Assert.Single(images);
        Assert.Null(images[0].Label);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task WriteInProgress_BlocksRevocationUntilCommit(string operation)
    {
        var factory = new Factory(await postgres.CreateEmptyDatabaseAsync());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var profile = new Profile { UserName = "owner", FullName = "Owner", PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { Name = "workspace" };
        var expense = new Expense { Workspace = workspace };
        db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
        await db.SaveChangesAsync();
        var store = new PostgresImageStore(factory);
        var image = (await store.SaveAsync(profile.PublicId, expense.PublicId, "image/png", [1], 1, 1)).Value!;
        var pause = new LockPause();
        factory.Pause = pause;
        var write = Write(store, operation, profile.PublicId, expense.PublicId, image.ImageId);
        try
        {
            await pause.Locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET lock_timeout = '500ms'");
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM workspace_memberships WHERE workspace_id = {workspace.Id} AND profile_id = {profile.Id}"));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        }
        finally
        {
            pause.Release.TrySetResult();
        }
        Assert.Equal(ImageStoreStatus.Success, await write.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM workspace_memberships WHERE workspace_id = {workspace.Id} AND profile_id = {profile.Id}"));
    }

    private static async Task<ImageStoreStatus> Write(PostgresImageStore store, string operation, Guid profileId, Guid expenseId, Guid imageId) => operation switch
    {
        "save" => (await store.SaveAsync(profileId, expenseId, "image/png", [2], 1, 1)).Status,
        "update" => (await store.UpdateAsync(profileId, expenseId, imageId, true, "Receipt")).Status,
        "delete" => await store.DeleteAsync(profileId, expenseId, imageId),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private sealed class LockPause
    {
        public TaskCompletionSource Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Factory(string connectionString) : IDbContextFactory<CompanionDbContext>
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public LockPause? Pause { get; set; }
        public TaskCompletionSource? LockExecuting { get; set; }
        public CompanionDbContext CreateDbContext()
        {
            var builder = new DbContextOptionsBuilder<CompanionDbContext>();
            builder.UseCompanionDatabase(connectionString);
            builder.AddInterceptors(new Recorder(Commands, Pause, LockExecuting));
            return new CompanionDbContext(builder.Options);
        }
    }

    private sealed class Recorder(ConcurrentQueue<string> commands, LockPause? pause, TaskCompletionSource? lockExecuting) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            commands.Enqueue(command.CommandText);
            if (command.CommandText.Contains("FOR UPDATE OF e", StringComparison.OrdinalIgnoreCase)) lockExecuting?.TrySetResult();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (pause != null && command.CommandText.Contains("FOR UPDATE OF e", StringComparison.OrdinalIgnoreCase))
            {
                pause.Locked.TrySetResult();
                await pause.Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}