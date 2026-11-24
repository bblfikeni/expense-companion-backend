using System.Net.Mail;

namespace Companion.Notification;

public static class NotificationEmailServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        if (!configuration.GetValue("Channels:Email:Enabled", true))
            return services;

        services.AddOptions<ResendOptions>()
            .BindConfiguration("Resend")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey) && !options.ApiKey.Any(char.IsWhiteSpace),
                "Resend:ApiKey must be set when email delivery is enabled.")
            .Validate(options => MailAddress.TryCreate(options.From, out _),
                "Resend:From must be a valid sender address when email delivery is enabled.")
            .ValidateOnStart();

        services.AddHttpClient<INotificationSender, ResendEmailProvider>(client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
                client.Timeout = Timeout.InfiniteTimeSpan; // The resilience handler owns timeouts.
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.UseJitter = true;
            });

        return services;
    }
}
