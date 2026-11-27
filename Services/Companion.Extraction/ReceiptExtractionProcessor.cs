using System.Security.Cryptography;
using System.Text;
using Companion.Data;
using Companion.Data.Models;
using Companion.Extraction.Configuration;
using Companion.Extraction.Localization;
using Companion.Extraction.Logic;
using Companion.Extraction.Ocr;
using Companion.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Companion.Extraction;

public sealed class ReceiptExtractionProcessor(
    IDbContextFactory<CompanionDbContext> contextFactory,
    IOcrEngine ocrEngine,
    IOptions<ExtractionOptions> options,
    TimeProvider clock,
    ILogger<ReceiptExtractionProcessor> logger) : IReceiptExtractionProcessor
{
    private readonly ExtractionOptions _options = options.Value;

    public async Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken)
    {
        while (true)
        {
            var claim = await ClaimAsync(message.ExpensePublicId, cancellationToken);
            if (claim is null) return;
            try
            {
                if (await ExtractAndPersistAsync(claim, cancellationToken)) return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var retry = await RecordFailureAsync(claim, cancellationToken);
                logger.LogError("Receipt processing failed for {ExpensePublicId} ({EventId}); retry {WillRetry}, failure type {FailureType}.",
                    message.ExpensePublicId, message.EventId, retry, exception.GetType().Name);
            }
        }
    }

    public async Task RecoverAsync(Guid expensePublicId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.MessageTimeoutSeconds)));
        var claim = (await TryClaimAsync(expensePublicId, timeout.Token)).Claim;
        if (claim is null) return;
        try
        {
            await ExtractAndPersistAsync(claim, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var retry = await RecordFailureAsync(claim, timeout.Token);
            logger.LogError("Receipt recovery failed for {ExpensePublicId}; retry {WillRetry}, failure type {FailureType}.",
                expensePublicId, retry, exception.GetType().Name);
        }
    }

    private async Task<ExtractionClaim?> ClaimAsync(Guid expensePublicId, CancellationToken ct)
    {
        while (true)
        {
            var decision = await TryClaimAsync(expensePublicId, ct);
            if (decision.Claim is not null || decision.WaitUntil is null) return decision.Claim;
            var delay = decision.WaitUntil.Value - clock.GetUtcNow();
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
        }
    }

    private async Task<ClaimDecision> TryClaimAsync(Guid expensePublicId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var expense = await db.Expenses.Where(e => e.PublicId == expensePublicId)
                .Select(e => new { e.Id, e.PublicId }).SingleOrDefaultAsync(ct);
            if (expense is null || !await LockProcessingExpenseAsync(db, expense.Id, ct))
            {
                await transaction.RollbackAsync(ct);
                return new ClaimDecision(null, null);
            }

            var images = await db.ExpenseImages.AsNoTracking().Where(i => i.ExpenseId == expense.Id)
                .OrderBy(i => i.PageNo)
                .Select(i => new ReceiptImage(i.Id, i.PageNo, i.Sha256, i.Label))
                .ToListAsync(ct);
            if (images.Count == 0 || images.Count > _options.MaxImagesPerExpense)
            {
                await transaction.RollbackAsync(ct);
                return new ClaimDecision(null, null);
            }

            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(images.Select(i => i.Sha256)))));
            var extraction = await db.ExpenseExtractions.SingleOrDefaultAsync(
                e => e.ExpenseId == expense.Id && e.ImageSetHash == hash, ct);
            var now = clock.GetUtcNow();
            var lease = ExtractionOptions.ProcessingLease;
            if (extraction?.CompletedAt is not null ||
                extraction is not null && extraction.Status is not ("Processing" or "RetryPending"))
            {
                await transaction.CommitAsync(ct);
                return new ClaimDecision(null, null);
            }
            if (extraction?.Status == "Processing" && now - extraction.StartedAt < lease)
            {
                await transaction.CommitAsync(ct);
                return new ClaimDecision(null, extraction.StartedAt.Add(lease));
            }

            if (extraction?.Status == "RetryPending" && extraction.NextAttemptAt is { } retryAt && retryAt > now)
            {
                await transaction.CommitAsync(ct);
                return new ClaimDecision(null, retryAt);
            }
            if (extraction is not null && extraction.AttemptCount >= Math.Max(1, _options.MaxRetries))
            {
                extraction.Status = "Failed";
                extraction.NextAttemptAt = null;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new ClaimDecision(null, null);
            }
            var ticks = now.UtcTicks - now.UtcTicks % 10;
            var token = new DateTimeOffset(ticks, TimeSpan.Zero);
            if (extraction is not null && token <= extraction.StartedAt)
                token = extraction.StartedAt.AddTicks(10);
            if (extraction is null)
            {
                extraction = new ExpenseExtraction
                {
                    ExpenseId = expense.Id,
                    ExpensePublicId = expense.PublicId,
                    ImageSetHash = hash,
                    OverallConfidence = 0,
                    ReviewRequired = true,
                    Status = "Processing",
                    StartedAt = token,
                    AttemptCount = 1,
                };
                db.ExpenseExtractions.Add(extraction);
            }
            else
            {
                extraction.Status = "Processing";
                extraction.StartedAt = token;
                extraction.NextAttemptAt = null;
                extraction.AttemptCount++;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new ClaimDecision(new ExtractionClaim(extraction.Id, expense.Id, expense.PublicId, images, token), null);
        }, ct);
    }

    private async Task<bool> ExtractAndPersistAsync(ExtractionClaim claim, CancellationToken ct)
    {
        var pages = new List<ProcessedPage>();
        foreach (var image in claim.Images)
        {
            ct.ThrowIfCancellationRequested();
            var ocr = await ExtractImageAsync(image, ct);
            var classification = DocClassifier.Classify(ocr.Text, new DocClassifierOptions());
            pages.Add(new ProcessedPage(image.PageNo, classification.DocType, image.Label, classification.CrossCheckReason, ocr));
        }

        var hasReceiptPage = pages.Any(p => p.Label == "Receipt" || ReceiptMerger.IsReceiptType(p.DocumentType));
        var receiptPages = pages.Where(p =>
            p.Label != "ProofOfPayment" && p.DocumentType is not ("EftConfirmation" or "CardMachineSlip") &&
            (p.Label == "Receipt" || ReceiptMerger.IsReceiptType(p.DocumentType) ||
             hasReceiptPage && p.DocumentType == "Unknown" && p.CrossCheckReason is null)).ToArray();
        var mergedText = ReceiptMerger.MergeI(receiptPages.SelectMany(p =>
        {
            foreach (var line in p.Ocr.Lines) line.PageNo = p.PageNo;
            return p.Ocr.Lines;
        }).ToList());
        var tokens = receiptPages.SelectMany(p => p.Ocr.Lines).SelectMany(line => line.Words).ToArray();
        var parsed = FieldParser.Parse(mergedText, tokens, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), _options);
        var overall = new[] { parsed.VendorConfidence, parsed.DateConfidence, parsed.TotalConfidence,
            parsed.VatAmountConfidence, parsed.VatNumberConfidence, parsed.DocumentValidityConfidence }.Average();

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockProcessingExpenseAsync(db, claim.ExpenseId, ct))
            return false;
        var extraction = await db.ExpenseExtractions.SingleAsync(e => e.Id == claim.ExtractionId, ct);
        if (extraction.Status != "Processing" ||
            extraction.CompletedAt is not null || extraction.StartedAt != claim.StartedAt)
            return false;
        extraction.Vendor = parsed.Vendor;
        extraction.ExpenseDate = parsed.Date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        extraction.TotalAmountCents = parsed.TotalCents;
        extraction.VatAmountCents = parsed.VatAmountCents;
        extraction.VatNumber = parsed.VatNumber;
        extraction.VatClassification = parsed.VatClassification;
        extraction.IsValidTaxInvoice = parsed.IsValidTaxInvoice;
        extraction.VendorConfidence = parsed.VendorConfidence;
        extraction.DateConfidence = parsed.DateConfidence;
        extraction.TotalConfidence = parsed.TotalConfidence;
        extraction.VatAmountConfidence = parsed.VatAmountConfidence;
        extraction.VatNumberConfidence = parsed.VatNumberConfidence;
        extraction.DocumentValidityConfidence = parsed.DocumentValidityConfidence;
        extraction.OverallConfidence = overall;
        extraction.ReviewRequired = true;
        extraction.Status = "NeedsReview";
        extraction.CompletedAt = clock.GetUtcNow();
        foreach (var page in pages)
            db.ExpenseImageResults.Add(new ExpenseImageResult
            {
                ExtractionId = extraction.Id,
                PageNo = page.PageNo,
                DocumentType = page.DocumentType,
                OcrEngine = "Tesseract",
                ImageQualityScore = page.Ocr.MeanConfidence,
                OcrMeanConfidence = page.Ocr.MeanConfidence,
            });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Receipt extraction stored for {ExpensePublicId} with {PageCount} pages; status NeedsReview.",
            claim.ExpensePublicId, pages.Count);
        return true;
        }, ct);
    }

    private async Task<OcrResult> ExtractImageAsync(ReceiptImage image, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var bytes = await db.ExpenseImageBlobs.AsNoTracking()
            .Where(blob => blob.ExpenseImageId == image.Id)
            .Select(blob => blob.Content)
            .SingleOrDefaultAsync(ct) ?? throw new InvalidDataException("Stored receipt image has no blob.");
        try
        {
            return await ocrEngine.ExtractAsync(bytes, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task<bool> RecordFailureAsync(ExtractionClaim claim, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockProcessingExpenseAsync(db, claim.ExpenseId, ct))
            return false;
        var extraction = await db.ExpenseExtractions.SingleOrDefaultAsync(e => e.Id == claim.ExtractionId, ct);
        if (extraction is null || extraction.Status != "Processing" ||
            extraction.CompletedAt is not null || extraction.StartedAt != claim.StartedAt) return false;
        if (extraction.AttemptCount >= Math.Max(1, _options.MaxRetries))
        {
            extraction.Status = "Failed";
            extraction.NextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return false;
        }
        var delay = Math.Clamp(_options.RetryBackoffBaseSeconds * Math.Pow(2, extraction.AttemptCount - 1), 0, 300);
        extraction.Status = "RetryPending";
        extraction.NextAttemptAt = clock.GetUtcNow().AddSeconds(delay);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
        }, ct);
    }

    private static Task<bool> LockProcessingExpenseAsync(CompanionDbContext db, int expenseId, CancellationToken ct)
        => db.Database.SqlQuery<bool>($"SELECT public.lock_extraction_expense({expenseId}) AS \"Value\"").SingleAsync(ct);

    private sealed record ReceiptImage(int Id, int PageNo, string Sha256, string? Label);
    private sealed record ProcessedPage(int PageNo, string DocumentType, string? Label, string? CrossCheckReason, OcrResult Ocr);
    private sealed record ClaimDecision(ExtractionClaim? Claim, DateTimeOffset? WaitUntil);
    private sealed record ExtractionClaim(int ExtractionId, int ExpenseId, Guid ExpensePublicId,
        IReadOnlyList<ReceiptImage> Images, DateTimeOffset StartedAt);
}
