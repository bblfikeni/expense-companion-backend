using System.Drawing;
using System.Security.Cryptography;
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
using Microsoft.Extensions.Options;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ReceiptExtractionProcessorTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ProcessesStoredImageAndDuplicateDeliveryDoesNotDuplicateResults()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var originalBytes = new byte[] { 10, 20, 30, 40 };
        Guid expensePublicId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "extractor", FullName = "Extractor", PopiaConsentDate = DateTimeOffset.UtcNow };
            var workspace = new Workspace { Name = "Receipt workspace" };
            var expense = new Expense { Workspace = workspace, Status = "Processing" };
            db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            expensePublicId = expense.PublicId;
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id,
                UploadedByProfileId = profile.Id,
                PageNo = 1,
                ContentType = "image/png",
                SizeBytes = originalBytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(originalBytes)),
                Width = 1,
                Height = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                Blob = new ExpenseImageBlob { Content = originalBytes },
            });
            await db.SaveChangesAsync();
        }

        var ocr = new MemoryCapturingOcrEngine();
        var settings = new ExtractionOptions { Enabled = true, RetryBackoffBaseSeconds = 0 };
        var processor = new ReceiptExtractionProcessor(factory, ocr, Microsoft.Extensions.Options.Options.Create(settings), TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        var message = new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = expensePublicId };

        await processor.ProcessAsync(message, CancellationToken.None);
        Assert.Equal(originalBytes, ocr.CapturedBytes);
        await processor.ProcessAsync(message, CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var extraction = await verify.ExpenseExtractions.SingleAsync();
        Assert.Equal("NeedsReview", extraction.Status);
        Assert.Equal(2, extraction.AttemptCount);
        Assert.True(extraction.ReviewRequired);
        Assert.NotNull(extraction.CompletedAt);
        Assert.Equal(10000, extraction.TotalAmountCents);
        Assert.Equal(1304, extraction.VatAmountCents);
        Assert.Equal("Demo Market", extraction.Vendor);
        Assert.Equal("2026-11-25", extraction.ExpenseDate);
        Assert.Single(await verify.ExpenseImageResults.ToListAsync());
        Assert.Equal("Processing", (await verify.Expenses.SingleAsync(e => e.PublicId == expensePublicId)).Status);
    }

    [Fact]
    public async Task ContinuationPageContributesReceiptTextButProofOfPaymentDoesNot()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        var texts = new Dictionary<byte, string>
        {
            [1] = "Demo Market\nTax Invoice\nDate: 25/11/2026",
            [2] = "Items continued\nTotal R100.00\nVAT R13.04",
            [3] = "Total R999.00",
        };
        Guid expensePublicId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "pages", FullName = "Pages", PopiaConsentDate = DateTimeOffset.UtcNow };
            var workspace = new Workspace { Name = "Pages" };
            var expense = new Expense { Workspace = workspace, Status = "Processing" };
            db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            expensePublicId = expense.PublicId;
            for (var pageNo = 1; pageNo <= texts.Count; pageNo++)
            {
                var bytes = new[] { (byte)pageNo, (byte)2, (byte)3 };
                db.ExpenseImages.Add(new ExpenseImage
                {
                    ExpenseId = expense.Id,
                    UploadedByProfileId = profile.Id,
                    PageNo = pageNo,
                    ContentType = "image/png",
                    SizeBytes = bytes.Length,
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    Label = pageNo == 3 ? "ProofOfPayment" : null,
                    Width = 1,
                    Height = 1,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Blob = new ExpenseImageBlob { Content = bytes },
                });
            }
            await db.SaveChangesAsync();
        }

        var ocr = new TextOcrEngine(texts);
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        await processor.ProcessAsync(new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = expensePublicId },
            CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var extraction = await verify.ExpenseExtractions.SingleAsync();
        Assert.Equal(10000, extraction.TotalAmountCents);
        Assert.Equal(1304, extraction.VatAmountCents);
        Assert.Equal("Demo Market", extraction.Vendor);
        Assert.Equal(3, await verify.ExpenseImageResults.CountAsync());
    }

    [Fact]
    public async Task ExpiredClaimAtRetryLimitFailsWithoutAnotherOcrAttempt()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        Guid expensePublicId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "crashed", FullName = "Crashed", PopiaConsentDate = DateTimeOffset.UtcNow };
            var expense = new Expense { Workspace = new Workspace { Name = "Crashed" }, Status = "Processing" };
            db.AddRange(profile, expense);
            await db.SaveChangesAsync();
            expensePublicId = expense.PublicId;
            var bytes = new byte[] { 9, 8, 7 };
            var imageHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            db.ExpenseImages.Add(new ExpenseImage
            {
                ExpenseId = expense.Id, UploadedByProfileId = profile.Id, PageNo = 1,
                ContentType = "image/png", SizeBytes = bytes.Length, Sha256 = imageHash,
                Width = 1, Height = 1, CreatedAt = DateTimeOffset.UtcNow,
                Blob = new ExpenseImageBlob { Content = bytes },
            });
            db.ExpenseExtractions.Add(new ExpenseExtraction
            {
                ExpenseId = expense.Id, ExpensePublicId = expense.PublicId,
                ImageSetHash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(imageHash))),
                Status = "Processing", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                AttemptCount = 2, OverallConfidence = 0, ReviewRequired = true,
            });
            await db.SaveChangesAsync();
        }

        var ocr = new CountingOcrEngine();
        var settings = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
        {
            Enabled = true, MaxRetries = 2, MessageTimeoutSeconds = 1,
        });
        var processor = new ReceiptExtractionProcessor(factory, ocr, settings, TimeProvider.System,
            NullLogger<ReceiptExtractionProcessor>.Instance);
        await processor.RecoverAsync(expensePublicId, CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var extraction = await verify.ExpenseExtractions.SingleAsync();
        Assert.Equal("Failed", extraction.Status);
        Assert.Equal(2, extraction.AttemptCount);
        Assert.Equal(0, ocr.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateWaitsForAbandonedLeaseAndStaleClaimCannotWrite(bool staleFails)
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<CompanionDbContext>>();
        Guid expensePublicId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var profile = new Profile { UserName = "claims", FullName = "Claims", PopiaConsentDate = DateTimeOffset.UtcNow };
            var workspace = new Workspace { Name = "Claims" };
            var expense = new Expense { Workspace = workspace, Status = "Processing" };
            db.AddRange(profile, expense, new WorkspaceMembership { Workspace = workspace, Profile = profile });
            await db.SaveChangesAsync();
            expensePublicId = expense.PublicId;
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

        var ocr = new BlockingOcrEngine(staleFails);
        var settings = new ExtractionOptions { MessageTimeoutSeconds = 10, RetryBackoffBaseSeconds = 0 };
        var processor = new ReceiptExtractionProcessor(factory, ocr, Microsoft.Extensions.Options.Options.Create(settings),
            TimeProvider.System, NullLogger<ReceiptExtractionProcessor>.Instance);
        var message = new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = expensePublicId };
        using var cancellation = new CancellationTokenSource();
        var stale = processor.ProcessAsync(message, cancellation.Token);
        try
        {
            await ocr.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await using (var db = await factory.CreateDbContextAsync())
            {
                var extraction = await db.ExpenseExtractions.SingleAsync();
                extraction.StartedAt = DateTimeOffset.UtcNow.AddSeconds(-319);
                await db.SaveChangesAsync();
            }
            var duplicate = processor.ProcessAsync(message, cancellation.Token);
            await Task.Delay(200);
            Assert.False(duplicate.IsCompleted);
            await ocr.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ocr.ReleaseSecond.TrySetResult();
            await duplicate.WaitAsync(TimeSpan.FromSeconds(5));
            ocr.ReleaseFirst.TrySetResult();
            await stale.WaitAsync(TimeSpan.FromSeconds(5));

            await using var verify = await factory.CreateDbContextAsync();
            var result = await verify.ExpenseExtractions.SingleAsync();
            Assert.Equal("NeedsReview", result.Status);
            Assert.Equal(2, result.AttemptCount);
            Assert.Single(await verify.ExpenseImageResults.ToListAsync());
        }
        finally
        {
            cancellation.Cancel();
            ocr.ReleaseFirst.TrySetResult();
            ocr.ReleaseSecond.TrySetResult();
            try { await stale; } catch (OperationCanceledException) { }
        }
    }

    private sealed class BlockingOcrEngine(bool staleFails) : IOcrEngine
    {
        private int _calls;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSecond { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                FirstStarted.TrySetResult();
                await ReleaseFirst.Task.WaitAsync(cancellationToken);
                if (staleFails) throw new IOException("Stale failure");
            }
            else
            {
                SecondStarted.TrySetResult();
                await ReleaseSecond.Task.WaitAsync(cancellationToken);
            }
            return new OcrResult { Text = "", MeanConfidence = 0, Lines = [] };
        }
    }

    private sealed class MemoryCapturingOcrEngine : IOcrEngine, IInMemoryOcrEngine
    {
        private int _fail = 1;
        public byte[]? CapturedBytes { get; private set; }
        private const string Text = "Demo Market\nTax Invoice\nDate: 25/11/2026\nTotal R100.00\nVAT R13.04";

        public Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("The in-memory OCR method should be selected.");
        }

        public Task<OcrResult> ExtractFromMemoryAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            CapturedBytes = imageBytes.ToArray();
            if (Interlocked.Exchange(ref _fail, 0) == 1) throw new IOException("Injected transient OCR failure");
            var lines = Text.Split('\n').Select(text => new OcrResult.OcrLineResult
            {
                Text = text,
                Confidence = 0.95,
                PageNo = 1,
                Words = text.Split(' ').Select(word => new OcrResult.OcrWordResult
                {
                    Text = word,
                    Confidence = 0.95,
                    BoundingBox = RectangleF.Empty,
                }).ToList(),
            }).ToList();
            return Task.FromResult(new OcrResult { Text = Text, MeanConfidence = 0.95, Lines = lines });
        }
    }

    private sealed class TextOcrEngine(IReadOnlyDictionary<byte, string> texts) : IOcrEngine
    {
        public async Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            var text = texts[bytes[0]];
            var lines = text.Split('\n').Select(line => new OcrResult.OcrLineResult
            {
                Text = line,
                Confidence = 0.95,
                Words = line.Split(' ').Select(word => new OcrResult.OcrWordResult
                {
                    Text = word, Confidence = 0.95, BoundingBox = RectangleF.Empty,
                }).ToList(),
            }).ToList();
            return new OcrResult { Text = text, MeanConfidence = 0.95, Lines = lines };
        }
    }

    private sealed class CountingOcrEngine : IOcrEngine
    {
        public int Calls { get; private set; }
        public Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new OcrResult { Text = string.Empty, MeanConfidence = 0, Lines = [] });
        }
    }
}