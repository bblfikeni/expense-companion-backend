using Companion.Configuration;
using Companion.Extraction;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);
builder.Services.AddHostedService<ExtractionWorker>();

var host = builder.Build();
host.Run();
