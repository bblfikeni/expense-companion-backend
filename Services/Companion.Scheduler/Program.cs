using Companion.Configuration;
using Companion.Scheduler;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);
builder.Services.AddHostedService<SchedulerWorker>();

var host = builder.Build();
host.Run();
