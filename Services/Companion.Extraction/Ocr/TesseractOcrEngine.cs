using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tesseract;

namespace Companion.Extraction.Ocr;

/// <summary>
/// Self-hosted OCR engine backed by Tesseract.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine, IInMemoryOcrEngine
{
    public static string DataPath { get; set; } = "/usr/share/tessdata";

    private readonly string _languages;
    private readonly ILogger<TesseractOcrEngine> _logger;

    public TesseractOcrEngine(string? languages = null, ILogger<TesseractOcrEngine>? logger = null)
    {
        _languages = languages ?? "eng+afr";
        _logger = logger ?? NullLogger<TesseractOcrEngine>.Instance;
    }

    internal string Languages => _languages;

    public Task<OcrResult> ExtractAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<OcrResult>(cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        return Task.Run(() => Extract(() => Pix.LoadFromFile(imagePath), cancellationToken), cancellationToken);
    }

    public Task<OcrResult> ExtractFromMemoryAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<OcrResult>(cancellationToken);
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
        return Task.Run(() => Extract(() => Pix.LoadFromMemory(imageBytes), cancellationToken), cancellationToken);
    }

    private OcrResult Extract(Func<Pix> loadImage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("OCR extraction started.");

        try
        {
            using var engine = new TesseractEngine(ResolveDataPath(DataPath), _languages, EngineMode.Default);
            using var image = loadImage();
            cancellationToken.ThrowIfCancellationRequested();
            using var page = engine.Process(image);

            var result = new OcrResult
            {
                Text = page.GetText() ?? string.Empty,
                MeanConfidence = NormalizePageConfidence(page.GetMeanConfidence())
            };
            AddLinesAndWords(page, image.Width, image.Height, result, cancellationToken);
            _logger.LogInformation("OCR extraction completed. Lines: {LineCount}; words: {WordCount}; elapsed_ms: {ElapsedMilliseconds}.",
                result.Lines.Count, result.Lines.Sum(line => line.Words.Count), stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OCR extraction canceled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("OCR extraction failed. Failure type: {FailureType}.", exception.GetType().Name);
            throw;
        }
    }

    private static void AddLinesAndWords(Page page, int imageWidth, int imageHeight, OcrResult result,
        CancellationToken cancellationToken)
    {
        using var iterator = page.GetIterator();
        iterator.Begin();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
            {
                var lineText = iterator.GetText(PageIteratorLevel.TextLine) ?? string.Empty;
                result.Lines.Add(new OcrResult.OcrLineResult { Text = lineText.Trim(), PageNo = 1 });
            }

            var wordText = iterator.GetText(PageIteratorLevel.Word);
            if (!string.IsNullOrWhiteSpace(wordText) && result.Lines.Count > 0)
            {
                var word = new OcrResult.OcrWordResult
                {
                    Text = wordText.Trim(),
                    Confidence = NormalizeWordConfidence(iterator.GetConfidence(PageIteratorLevel.Word)),
                    BoundingBox = GetNormalizedBounds(iterator, imageWidth, imageHeight)
                };
                result.Lines.Last().Words.Add(word);
            }
        } while (iterator.Next(PageIteratorLevel.Word));

        foreach (var line in result.Lines.Where(line => line.Words.Count > 0))
            line.Confidence = line.Words.Average(word => word.Confidence);
    }

    private static RectangleF GetNormalizedBounds(ResultIterator iterator, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 ||
            !iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            return RectangleF.Empty;

        var left = Math.Clamp(bounds.X1 / (float)imageWidth, 0, 1);
        var top = Math.Clamp(bounds.Y1 / (float)imageHeight, 0, 1);
        var right = Math.Clamp(bounds.X2 / (float)imageWidth, left, 1);
        var bottom = Math.Clamp(bounds.Y2 / (float)imageHeight, top, 1);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    internal static double NormalizePageConfidence(float confidence) => Math.Clamp((double)confidence, 0d, 1d);

    internal static double NormalizeWordConfidence(float confidence) => Math.Clamp(confidence / 100d, 0d, 1d);

    internal static string ResolveDataPath(string configuredPath)
    {
        return Path.GetFullPath(configuredPath);
    }
}