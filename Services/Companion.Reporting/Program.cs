using Companion.Reporting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<ReportingWorker>();

var host = builder.Build();
host.Run();
