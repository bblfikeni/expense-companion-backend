using EasyNetQ;

namespace Companion.Messaging;

internal sealed class EasyNetQCompanionMessageBus(IBus bus) : ICompanionMessageBus
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        return bus.PubSub.PublishAsync(
            message,
            configuration => configuration.WithTopic(typeof(T).Name),
            cancellationToken);
    }

    public async Task<IAsyncDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(handler);

        return await bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            configuration => configuration.WithTopic(typeof(T).Name),
            cancellationToken);
    }
}
