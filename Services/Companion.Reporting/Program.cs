using Companion.Configuration;
using Companion.Reporting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);
builder.Services.AddHostedService<ReportingWorker>();

var host = builder.Build();
host.Run();
