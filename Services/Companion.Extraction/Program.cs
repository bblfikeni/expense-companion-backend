using Companion.Configuration;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Extraction.Ocr;
using Companion.Messaging;
using Companion.Data.Extensions;
using Microsoft.Extensions.Options;

namespace Companion.Extraction;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddCompanionAppSettings(builder.Environment);

        var extractionEnabled = builder.Configuration.GetValue("Extraction:Enabled", false);
        var extractionOptions = builder.Services.AddOptions<ExtractionOptions>()
            .Bind(builder.Configuration.GetSection("Extraction"));
        if (extractionEnabled)
        {
            extractionOptions
                .Validate(options => ExtractionOptions.HasCompatibleImageLimit(options.MaxImagesPerExpense),
                    $"Extraction:MaxImagesPerExpense must equal the gateway limit of {ExtractionOptions.ApiMaximumImagesPerExpense}.")
                .ValidateOnStart();
        }
        if (extractionEnabled)
        {
            builder.Services.AddCompanionPostgres(builder.Configuration);
            builder.Services.AddCompanionMessaging(builder.Configuration);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
            builder.Services.AddSingleton<ReceiptExtractionProcessor>();
            builder.Services.AddSingleton<IReceiptExtractionProcessor>(sp => sp.GetRequiredService<ReceiptExtractionProcessor>());
            builder.Services.AddHostedService<ExtractionWorker>();
            builder.Services.AddHostedService<ReceiptRecoveryWorker>();
        }

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Companion.Extraction");
        var enabled = host.Services.GetRequiredService<IOptions<ExtractionOptions>>().Value.Enabled;
        logger.LogInformation("Extraction service started. Extraction enabled: {Enabled}.", enabled);
        if (!enabled)
            logger.LogInformation("Extraction is disabled; no database, broker or receipt subscription was initialized.");
        host.Run();
    }
}
