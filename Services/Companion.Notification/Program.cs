using Companion.Notification;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();
