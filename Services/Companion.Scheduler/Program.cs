using Companion.Scheduler;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<SchedulerWorker>();

var host = builder.Build();
host.Run();
