using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Companion.Events;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Messaging;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Companion.Api.Tests;

public sealed class ExtractionRabbitMqTests
{
    [Fact]
    public async Task WorkerRetriesInDeliveryWhileEscapedHandlerErrorsGoToErrorQueue()
    {
        await using var rabbit = new ContainerBuilder("rabbitmq:4.0-management-alpine")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "companion-test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "companion-test-password")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithTmpfsMount("/var/lib/rabbitmq")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();
        await rabbit.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCompanionMessaging(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:ConnectionString"] = $"host={rabbit.Hostname};port={rabbit.GetMappedPublicPort(5672)};username=companion-test;password=companion-test-password",
        }).Build());
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<ICompanionMessageBus>();
        var gatedBus = new SubscriptionGate(bus);
        var processor = new TransientFailureProcessor();
        var worker = new ExtractionWorker(gatedBus, processor,
            Microsoft.Extensions.Options.Options.Create(new ExtractionOptions { Enabled = true, RetryBackoffBaseSeconds = 0 }),
            NullLogger<ExtractionWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await gatedBus.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var message = new ReceiptUploadedEvent { EventId = Guid.NewGuid(), ExpensePublicId = Guid.NewGuid() };
            await bus.PublishAsync(message);
            Assert.Equal(message.EventId, (await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15))).EventId);
            Assert.Equal(2, processor.Calls);

            var errors = 0;
            var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var subscription = await bus.SubscribeAsync<EmailVerificationRequested>("extraction-error-regression", (_, _) =>
            {
                Interlocked.Increment(ref errors);
                failed.TrySetResult();
                throw new InvalidOperationException("Injected handler error");
            });
            await bus.PublishAsync(new EmailVerificationRequested { EventId = Guid.NewGuid(), Email = "test@example.com", OtpCode = "123456" });
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(15));

            using var client = new HttpClient { BaseAddress = new Uri($"http://{rabbit.Hostname}:{rabbit.GetMappedPublicPort(15672)}") };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("companion-test:companion-test-password")));
            string? errorQueue = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (errorQueue is null)
            {
                using var response = await client.GetAsync("/api/queues", timeout.Token);
                response.EnsureSuccessStatusCode();
                using var queues = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                foreach (var queue in queues.RootElement.EnumerateArray())
                {
                    var name = queue.GetProperty("name").GetString();
                    if (name is not null && name.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                        queue.TryGetProperty("messages_ready", out var ready) && ready.GetInt32() > 0)
                        errorQueue = name;
                }
                if (errorQueue is null) await Task.Delay(200, timeout.Token);
            }
            Assert.Equal(1, Volatile.Read(ref errors));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class TransientFailureProcessor : IReceiptExtractionProcessor
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource<ReceiptUploadedEvent> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ProcessAsync(ReceiptUploadedEvent message, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1) throw new IOException("Injected transient failure");
            Completed.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SubscriptionGate(ICompanionMessageBus inner) : ICompanionMessageBus
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => inner.PublishAsync(message, cancellationToken);

        public async Task<IAsyncDisposable> SubscribeAsync<T>(string subscriptionId,
            Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class
        {
            var subscription = await inner.SubscribeAsync(subscriptionId, handler, cancellationToken);
            Ready.TrySetResult();
            return subscription;
        }
    }
}