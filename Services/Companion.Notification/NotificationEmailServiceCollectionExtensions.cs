using System.Net;
using System.Net.Mail;
using Companion.Messaging;
using EasyNetQ.ConnectionString;
using Microsoft.Extensions.Options;

namespace Companion.Notification;

public static class NotificationEmailServiceCollectionExtensions
{
    private const string PickupVirtualHost = "companion-dev-mail";

    public static IServiceCollection AddNotificationEmail(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        if (!configuration.GetValue("Channels:Email:Enabled", true))
            return services;

        var pickupPath = configuration["Channels:Email:PickupDirectory"];
        if (!string.IsNullOrWhiteSpace(pickupPath))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Channels:Email:PickupDirectory is supported only in Development.");

            var directory = Path.GetFullPath(pickupPath, environment.ContentRootPath);
            services.AddSingleton<INotificationSender>(provider =>
            {
                ValidateEmailBroker(provider, pickup: true);
                DevelopmentEmailPickupDirectory.Create(directory);
                return new DevelopmentEmailPickupSender(directory);
            });
            return services;
        }

        services.AddOptions<ResendOptions>()
            .BindConfiguration("Resend")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey) && !options.ApiKey.Any(char.IsWhiteSpace),
                "Resend:ApiKey must be set when email delivery is enabled.")
            .Validate(options => MailAddress.TryCreate(options.From, out _),
                "Resend:From must be a valid sender address when email delivery is enabled.")
            .ValidateOnStart();

        services.AddHttpClient(nameof(INotificationSender), client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
                client.Timeout = Timeout.InfiniteTimeSpan; // The resilience handler owns timeouts.
            })
            .AddTypedClient<INotificationSender>((client, provider) =>
            {
                ValidateEmailBroker(provider, pickup: false);
                return new ResendEmailProvider(client, provider.GetRequiredService<IOptions<ResendOptions>>());
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

    private static void ValidateEmailBroker(IServiceProvider provider, bool pickup)
    {
        var options = provider.GetRequiredService<CompanionMessagingOptions>();
        var connection = provider.GetRequiredService<IConnectionStringParser>().Parse(options.ConnectionString);
        if (pickup)
        {
            if (connection.VirtualHost != PickupVirtualHost ||
                connection.Hosts.Count == 0 || connection.Hosts.Any(endpoint => !IsLoopback(endpoint.Host)))
                throw new InvalidOperationException(
                    $"Development mail pickup requires a loopback RabbitMQ broker and virtual host '{PickupVirtualHost}'.");
        }
        else if (connection.VirtualHost == PickupVirtualHost)
        {
            throw new InvalidOperationException(
                $"RabbitMQ virtual host '{PickupVirtualHost}' is reserved for development mail pickup.");
        }
    }

    private static bool IsLoopback(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
           || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
