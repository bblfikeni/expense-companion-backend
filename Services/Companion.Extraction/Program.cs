using Companion.Configuration;
using Companion.Extraction.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);

// Register extraction options, bounded to the Extracted: section of appsettings
builder.Services.Configure<ExtractionOptions>(
    builder.Configuration.GetSection("Extraction"));

var host = builder.Build();
host.Run();
