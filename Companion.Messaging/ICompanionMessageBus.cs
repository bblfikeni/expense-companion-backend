namespace Companion.Messaging;

public interface ICompanionMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    Task<IAsyncDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where T : class;
}
