using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ReceiptRecoveryWorkerTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SweepRecoversAbandonedWorkAndLeavesIneligibleReceiptsAlone()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var ids = new Dictionary<string, Guid>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "recovery", FullName = "Recovery", PopiaConsentDate = now };
            var workspace = new Workspace { Name = "Recovery" };
            db.AddRange(profile, workspace, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            foreach (var name in new[] { "missing", "expired", "retry", "active", "future", "completed", "failed", "draft", "noimage" })
            {
                var expense = new Expense { WorkspaceId = workspace.Id, Status = name == "draft" ? "None" : "Processing" };
                db.Expenses.Add(expense);
                await db.SaveChangesAsync();
                ids[name] = expense.PublicId;
                db.OutboxMessages.Add(ReceiptEvent(expense.PublicId, now));
                if (name == "noimage") continue;
                var bytes = Encoding.UTF8.GetBytes(name);
                var imageHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                db.ExpenseImages.Add(new ExpenseImage
                {
                    ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                    ContentType = "image/png", SizeBytes = bytes.Length, Sha256 = imageHash,
                    Width = 1, Height = 1, CreatedAt = now, Blob = new ExpenseImageBlob { Content = bytes },
                });
                if (name is not ("missing" or "draft"))
                    db.ExpenseExtractions.Add(new ExpenseExtraction
                    {
                        ExpenseId = expense.Id, ExpensePublicId = expense.PublicId,
                        ImageSetHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(imageHash))),
                        Status = name switch { "retry" or "future" => "RetryPending", "completed" => "NeedsReview", "failed" => "Failed", _ => "Processing" },
                        StartedAt = name == "active" ? now : now.AddHours(-1),
                        NextAttemptAt = name == "future" ? now.AddHours(1) : name == "retry" ? now.AddMinutes(-1) : null,
                        CompletedAt = name == "completed" ? now : null,
                        ReviewRequired = true,
                    });
            }
            await db.SaveChangesAsync();
        }

        var ocr = new CountingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System, NullLogger<ReceiptExtractionProcessor>.Instance);
        var worker = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await using var db = await factory.CreateDbContextAsync(timeout.Token);
                if (await db.ExpenseImageResults.CountAsync(timeout.Token) == 3) break;
                await Task.Delay(100, timeout.Token);
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
        await worker.SweepOnceAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));

        await using var verify = await factory.CreateDbContextAsync();
        var rows = await verify.ExpenseExtractions.ToDictionaryAsync(e => e.ExpensePublicId);
        foreach (var name in new[] { "missing", "expired", "retry", "completed" })
            Assert.Equal("NeedsReview", rows[ids[name]].Status);
        Assert.Equal("Processing", rows[ids["active"]].Status);
        Assert.Equal("RetryPending", rows[ids["future"]].Status);
        Assert.Equal("Failed", rows[ids["failed"]].Status);
        Assert.False(rows.ContainsKey(ids["draft"]));
        Assert.False(rows.ContainsKey(ids["noimage"]));
        Assert.Equal(3, ocr.Calls);
        Assert.Equal(3, await verify.ExpenseImageResults.CountAsync());
    }

    [Fact]
    public async Task DisabledSweepDoesNotAccessDatabase()
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = false });
        var worker = new ReceiptRecoveryWorker(null!, null!, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await worker.SweepOnceAsync(CancellationToken.None);
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SweepRotatesPagesAndConcurrentSweeperDoesNotTakeActiveClaim()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "paging", FullName = "Paging", PopiaConsentDate = DateTimeOffset.UtcNow };
            var workspace = new Workspace { Name = "Paging" };
            db.AddRange(profile, workspace, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            for (var i = 0; i < 100; i++) db.Expenses.Add(new Expense { WorkspaceId = workspace.Id, Status = "Processing" });
            await db.SaveChangesAsync();
            foreach (var existing in await db.Expenses.ToListAsync())
                db.OutboxMessages.Add(ReceiptEvent(existing.PublicId, DateTimeOffset.UtcNow));
            var expense = new Expense { WorkspaceId = workspace.Id, Status = "Processing" };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            db.OutboxMessages.Add(ReceiptEvent(expense.PublicId, DateTimeOffset.UtcNow));
            var bytes = new byte[] { 1, 2, 3 };
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), Width = 1, Height = 1,
                CreatedAt = DateTimeOffset.UtcNow, Blob = new ExpenseImageBlob { Content = bytes },
            });
            await db.SaveChangesAsync();
        }

        var ocr = new BlockingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System, NullLogger<ReceiptExtractionProcessor>.Instance);
        var first = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        var second = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await first.SweepOnceAsync(CancellationToken.None);
        Assert.Equal(0, ocr.Calls);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var active = first.SweepOnceAsync(cancellation.Token);
        try
        {
            await ocr.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await second.SweepOnceAsync(cancellation.Token);
            await second.SweepOnceAsync(cancellation.Token);
            Assert.Equal(1, ocr.Calls);
        }
        finally
        {
            ocr.Release.TrySetResult();
            await active;
        }
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Equal("NeedsReview", (await verify.ExpenseExtractions.SingleAsync()).Status);
        Assert.Single(await verify.ExpenseImageResults.ToListAsync());
    }

    [Fact]
    public async Task SweepFiltersCompletedReceiptsBeforePaging()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var now = DateTimeOffset.UtcNow;
        Guid eligiblePublicId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "terminal", FullName = "Terminal", PopiaConsentDate = now };
            var workspace = new Workspace { Name = "Terminal" };
            db.AddRange(profile, workspace, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            for (var i = 0; i < 100; i++)
                db.Expenses.Add(new Expense { WorkspaceId = workspace.Id, Status = "Processing" });
            await db.SaveChangesAsync();
            foreach (var expense in await db.Expenses.ToListAsync())
            {
                var bytes = BitConverter.GetBytes(expense.Id);
                db.OutboxMessages.Add(ReceiptEvent(expense.PublicId, now));
                db.ExpenseExtractions.Add(new ExpenseExtraction
                {
                    ExpenseId = expense.Id, ExpensePublicId = expense.PublicId,
                    ImageSetHash = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    Status = "NeedsReview", StartedAt = now, CompletedAt = now,
                    OverallConfidence = 0, ReviewRequired = true,
                });
            }

            var eligible = new Expense { WorkspaceId = workspace.Id, Status = "Processing" };
            db.Expenses.Add(eligible);
            await db.SaveChangesAsync();
            eligiblePublicId = eligible.PublicId;
            db.OutboxMessages.Add(ReceiptEvent(eligible.PublicId, now));
            var imageBytes = new byte[] { 80, 81, 82 };
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = eligible.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = imageBytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(imageBytes)), Width = 1, Height = 1,
                CreatedAt = now, Blob = new ExpenseImageBlob { Content = imageBytes },
            });
            await db.SaveChangesAsync();
        }

        var ocr = new CountingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        var worker = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await worker.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(1, ocr.Calls);
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Equal("NeedsReview", (await verify.ExpenseExtractions.SingleAsync(e => e.ExpensePublicId == eligiblePublicId)).Status);
        Assert.Equal(1, await verify.ExpenseImageResults.CountAsync());
    }

    [Fact]
    public async Task FailedAttemptIsRetriedOnLaterSweep()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "retry", FullName = "Retry", PopiaConsentDate = DateTimeOffset.UtcNow };
            var expense = new Expense { Workspace = new Workspace { Name = "Retry" }, Status = "Processing" };
            db.AddRange(profile, expense);
            await db.SaveChangesAsync();
            db.OutboxMessages.Add(ReceiptEvent(expense.PublicId, DateTimeOffset.UtcNow));
            var bytes = new byte[] { 4, 5, 6 };
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), Width = 1, Height = 1,
                CreatedAt = DateTimeOffset.UtcNow, Blob = new ExpenseImageBlob { Content = bytes },
            });
            await db.SaveChangesAsync();
        }
        var ocr = new FailOnceOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true, RetryBackoffBaseSeconds = 0 });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System, NullLogger<ReceiptExtractionProcessor>.Instance);
        var worker = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await worker.SweepOnceAsync(CancellationToken.None);
        await using (var db = await factory.CreateDbContextAsync())
            Assert.Equal("RetryPending", (await db.ExpenseExtractions.SingleAsync()).Status);
        await worker.SweepOnceAsync(CancellationToken.None);
        await using var verify = await factory.CreateDbContextAsync();
        var extraction = await verify.ExpenseExtractions.SingleAsync();
        Assert.Equal("NeedsReview", extraction.Status);
        Assert.Equal(2, extraction.AttemptCount);
        Assert.Equal(2, ocr.Calls);
    }

    [Fact]
    public async Task SweepRequiresPublishedMatchingReceiptEvent()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        int messageId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var now = DateTimeOffset.UtcNow;
            var profile = new Profile { UserName = "publication", FullName = "Publication", PopiaConsentDate = now };
            var expense = new Expense { Workspace = new Workspace { Name = "Publication" }, Status = "Processing" };
            db.AddRange(profile, expense);
            await db.SaveChangesAsync();
            var bytes = new byte[] { 7, 8, 9 };
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), Width = 1, Height = 1,
                CreatedAt = now, Blob = new ExpenseImageBlob { Content = bytes },
            });
            var pending = ReceiptEvent(expense.PublicId, null);
            db.OutboxMessages.AddRange(pending, ReceiptEvent(Guid.NewGuid(), now), new OutboxMessage
            {
                Type = "OtherEvent", Payload = pending.Payload, CreatedAt = now, PublishedAt = now,
            }, new OutboxMessage
            {
                Type = nameof(ReceiptUploadedEvent), Payload = JsonSerializer.Serialize(new { otherId = expense.PublicId }),
                CreatedAt = now, PublishedAt = now,
            });
            await db.SaveChangesAsync();
            messageId = pending.Id;
        }

        var ocr = new CountingOcr();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System, NullLogger<ReceiptExtractionProcessor>.Instance);
        var worker = new ReceiptRecoveryWorker(factory, processor, settings, NullLogger<ReceiptRecoveryWorker>.Instance);
        await worker.SweepOnceAsync(CancellationToken.None);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Empty(await db.ExpenseExtractions.ToListAsync());
            Assert.Equal(0, ocr.Calls);
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var message = await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
                message.PublishedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                await worker.SweepOnceAsync(CancellationToken.None);
                Assert.Empty(await db.ExpenseExtractions.ToListAsync());
                Assert.Equal(0, ocr.Calls);
                await transaction.CommitAsync();
            });
        }
        await worker.SweepOnceAsync(CancellationToken.None);
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Equal("NeedsReview", (await verify.ExpenseExtractions.SingleAsync()).Status);
        Assert.Single(await verify.ExpenseImageResults.ToListAsync());
        Assert.Equal(1, ocr.Calls);
    }

    private static OutboxMessage ReceiptEvent(Guid expensePublicId, DateTimeOffset? publishedAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new OutboxMessage
        {
            Type = nameof(ReceiptUploadedEvent),
            Payload = JsonSerializer.Serialize(new ReceiptUploadedEvent
            {
                ExpensePublicId = expensePublicId, EventId = Guid.NewGuid(), RequestedAt = now, OccurredAtUtc = now,
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAt = now, PublishedAt = publishedAt,
        };
    }

    private sealed class BlockingOcr : IOcrEngine
    {
        private int _calls;
        public int Calls => _calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new OcrResult { Text = "", MeanConfidence = 0, Lines = [] };
        }
    }

    private sealed class FailOnceOcr : IOcrEngine
    {
        public int Calls { get; private set; }
        public Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            if (++Calls == 1) throw new IOException("Injected failure");
            return Task.FromResult(new OcrResult { Text = "", MeanConfidence = 0, Lines = [] });
        }
    }

    private sealed class CountingOcr : IOcrEngine
    {
        public int Calls { get; private set; }
        public Task<OcrResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new OcrResult { Text = "", MeanConfidence = 0, Lines = [] });
        }
    }
}