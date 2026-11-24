using Companion.Configuration;
using Companion.Messaging;
using Companion.Notification;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCompanionAppSettings(builder.Environment);
// The built-in error strategy logs the original delivery body, including OTPs.
// Keep its error-queue handling and log auth delivery failures in NotificationWorker.
builder.Logging.AddFilter("EasyNetQ.Consumer.DefaultConsumeErrorStrategy", LogLevel.None);
builder.Services.AddCompanionMessaging(builder.Configuration);
builder.Services.AddNotificationEmail(builder.Configuration);
builder.Services.AddSingleton<NotificationTemplateRenderer>();
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();
