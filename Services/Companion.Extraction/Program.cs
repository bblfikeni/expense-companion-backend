using Companion.Configuration;
using Companion.Extraction.Configuration;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);

// Register extraction options, bounded to the Extracted: section of appsettings
builder.Services.Configure<ExtractionOptions>(
    builder.Configuration.GetSection("Extraction"));

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Companion.Extraction");
var enabled = host.Services.GetRequiredService<IOptions<ExtractionOptions>>().Value.Enabled;
logger.LogWarning("Extraction service started. Extraction enabled: {Enabled}; no extraction worker is registered, so no receipts will be processed.", enabled);
host.Run();
