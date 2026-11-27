using System;

namespace Companion.Extraction.Configuration;

/// <summary>
/// Configuration for the extraction pipeline.
/// All values are opt-in; nothing is enabled by default.
/// </summary>
[Serializable]
public class ExtractionOptions
{
    public const int ApiMaximumImagesPerExpense = 10;

    public static bool HasCompatibleImageLimit(int maximumImages) => maximumImages == ApiMaximumImagesPerExpense;
    /// <summary>
    /// When false, the ExtractionWorker must not subscribe or process any messages.
    /// When true, normal extraction processing occurs.
    /// Default: false (safety gate).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Time budget in seconds for a receipt delivery, including claim waits and retries. The default of
    /// 120s is less than half the default 540s recovery lease.
    /// </summary>
    public int MessageTimeoutSeconds { get; set; } = 120;

    internal static readonly TimeSpan ProcessingLease = TimeSpan.FromSeconds(540);
    // Leave room for process cleanup before half the lease has elapsed.
    public const int MaxMessageTimeoutSeconds = 268;

    public string OcrExecutablePath { get; set; } = "tesseract";
    public int OcrPageSegmentationMode { get; set; } = 6;
    public int OcrTimeoutSeconds { get; set; } = 30;
    public int OcrMaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Maximum number of retries for transient failures.
    /// Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base backoff delay in seconds between retries.
    /// Default: 30.
    /// </summary>
    public int RetryBackoffBaseSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of images per expense. This must match the gateway upload limit.
    /// Default: 10.
    /// </summary>
    public int MaxImagesPerExpense { get; set; } = ApiMaximumImagesPerExpense;

    /// <summary>
    /// Absolute VAT classification tolerance in cents. Default: 5.
    /// </summary>
    public long VatToleranceCents { get; set; } = 5;

    /// <summary>
    /// Overall confidence threshold for AutoQualify.
    /// Default: 0.85.
    /// </summary>
    public double AutoQualifyThreshold { get; set; } = 0.85;

    /// <summary>
    /// Overall confidence threshold for NeedsReview.
    /// Default: 0.60.
    /// </summary>
    public double NeedsReviewThreshold { get; set; } = 0.60;

    /// <summary>
    /// Minimum per-field confidence for total and date that caps overall confidence.
    /// Default: 0.50.
    /// </summary>
    public double CriticalFieldMinimum { get; set; } = 0.50;
}
