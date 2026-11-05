using EasyNetQ;
using EasyNetQ.ConnectionString;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.Messaging;

public static class CompanionMessagingServiceCollectionExtensions
{
    public static IServiceCollection AddCompanionMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var rabbitMqSection = configuration.GetSection("RabbitMq");
        var options = new CompanionMessagingOptions();
        options.ConnectionString = rabbitMqSection["ConnectionString"] ?? string.Empty;
        var pollingInterval = rabbitMqSection["OutboxPollingIntervalMilliseconds"];
        if (pollingInterval is not null)
        {
            if (!int.TryParse(pollingInterval, out var pollingIntervalMilliseconds))
                throw new InvalidOperationException(
                    "RabbitMq:OutboxPollingIntervalMilliseconds must be a positive integer.");
            options.OutboxPollingIntervalMilliseconds = pollingIntervalMilliseconds;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException(
                "RabbitMq:ConnectionString is required. Set RabbitMq__ConnectionString.");
        if (options.OutboxPollingIntervalMilliseconds <= 0)
            throw new InvalidOperationException("RabbitMq:OutboxPollingIntervalMilliseconds must be positive.");

        services.AddSingleton(options);
        services.AddEasyNetQ(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionStringParser>().Parse(options.ConnectionString);
            // A successful publish must mean the broker accepted a routed,
            // persistent message, even if the connection string disables these.
            connection.PublisherConfirms = true;
            connection.MandatoryPublish = true;
            connection.PersistentMessages = true;
            return connection;
        }).UseSystemTextJson();
        services.AddSingleton<ICompanionMessageBus, EasyNetQCompanionMessageBus>();

        return services;
    }
}
