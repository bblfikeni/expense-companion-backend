namespace Companion.Messaging;

public sealed class CompanionMessagingOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public int OutboxPollingIntervalMilliseconds { get; set; } = 1000;
}
