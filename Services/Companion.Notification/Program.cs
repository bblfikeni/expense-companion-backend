using Companion.Configuration;
using Companion.Messaging;
using Companion.Notification;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);
builder.Services.AddCompanionMessaging(builder.Configuration);
builder.Services.AddSingleton<NotificationTemplateRenderer>();
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();
