using Companion.Extraction;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<ExtractionWorker>();

var host = builder.Build();
host.Run();
