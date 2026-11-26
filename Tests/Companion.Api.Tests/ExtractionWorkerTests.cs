using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Companion.Api.Tests;

public sealed class ExtractionWorkerTests
{
    [Fact]
    public async Task DisabledWorkerDoesNotSubscribe()
    {
        var bus = new RecordingBus();
        var worker = CreateWorker(bus, new RecordingProcessor(), enabled: false);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.Equal(0, bus.SubscriptionCalls);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnabledWorkerSubscribesAndProcessesReceiptEvent()
    {
        var bus = new RecordingBus();
        var processor = new RecordingProcessor();
        var worker = CreateWorker(bus, processor, enabled: true);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await bus.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var message = new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() };
            await bus.DeliverAsync(message);
            Assert.Same(message, await processor.Processed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, bus.SubscriptionCalls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnexpectedProcessorFailureIsRetriedWithinDelivery()
    {
        var bus = new RecordingBus();
        var processor = new RecordingProcessor { FailuresRemaining = 1 };
        var worker = CreateWorker(bus, processor, enabled: true);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await bus.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await bus.DeliverAsync(new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() })
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, processor.Calls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnexpectedProcessorFailuresStopAtConfiguredRetryLimit()
    {
        var bus = new RecordingBus();
        var processor = new RecordingProcessor { FailuresRemaining = int.MaxValue };
        var worker = new ExtractionWorker(bus, processor,
            Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
            {
                Enabled = true, MaxRetries = 2, RetryBackoffBaseSeconds = 0,
            }), NullLogger<ExtractionWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await bus.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<IOException>(() => bus.DeliverAsync(
                new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() })
                .WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, processor.Calls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancellationStopsLocalRetry()
    {
        var bus = new RecordingBus();
        var processor = new RecordingProcessor { FailuresRemaining = 10 };
        var worker = CreateWorker(bus, processor, enabled: true);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await bus.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var delivery = bus.DeliverAsync(new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() },
                cancellation.Token);
            await processor.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
            Assert.Equal(1, processor.Calls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private static ExtractionWorker CreateWorker(RecordingBus bus, RecordingProcessor processor, bool enabled)
        => new(bus, processor, Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = enabled, RetryBackoffBaseSeconds = 0 }),
            NullLogger<ExtractionWorker>.Instance);

    private sealed class RecordingProcessor : IReceiptExtractionProcessor
    {
        public int FailuresRemaining { get; set; }
        public int Calls { get; private set; }
        public TaskCompletionSource<ReceiptUploadedEvent> Processed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailuresRemaining-- > 0)
            {
                Failed.TrySetResult();
                throw new IOException("Injected failure");
            }
            Processed.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBus : ICompanionMessageBus
    {
        private Func<ReceiptUploadedEvent, CancellationToken, Task>? _handler;
        public int SubscriptionCalls { get; private set; }
        public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;

        public Task<IAsyncDisposable> SubscribeAsync<T>(string subscriptionId,
            Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class
        {
            Assert.Equal("companion-extraction-receipt-uploaded", subscriptionId);
            SubscriptionCalls++;
            _handler = (message, ct) => handler((T)(object)message, ct);
            Subscribed.TrySetResult();
            return Task.FromResult<IAsyncDisposable>(new AsyncDisposable());
        }

        public Task DeliverAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken = default)
            => (_handler ?? throw new InvalidOperationException("Subscription is not ready."))(message, cancellationToken);

        private sealed class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}