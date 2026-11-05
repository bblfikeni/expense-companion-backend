using Companion.Events;
using Companion.Messaging;
using DotNet.Testcontainers.Builders;
using EasyNetQ;
using EasyNetQ.Producer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.Api.Tests;

public sealed class MessagingTests
{
    [Fact]
    public async Task Publishing_RejectsUnroutableMessages_ThenConfirmsDeliveryToDurableSubscription()
    {
        await using var rabbit = new ContainerBuilder("rabbitmq:4.0-management-alpine")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "companion-test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "companion-test-password")
            .WithPortBinding(5672, true)
            .WithTmpfsMount("/var/lib/rabbitmq")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();
        await rabbit.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCompanionMessaging(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:ConnectionString"] = $"host={rabbit.Hostname};port={rabbit.GetMappedPublicPort(5672)};username=companion-test;password=companion-test-password;publisherConfirms=false;mandatoryPublish=false;persistentMessages=false",
        }).Build());
        await using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<ConnectionConfiguration>();
        Assert.True(configuration.PublisherConfirms);
        Assert.True(configuration.MandatoryPublish);
        Assert.True(configuration.PersistentMessages);
        var bus = provider.GetRequiredService<ICompanionMessageBus>();
        var message = new EmailVerificationRequested { EventId = Guid.NewGuid(), Email = "person@example.com", OtpCode = "123456" };

        await Assert.ThrowsAsync<PublishReturnedException>(() => bus.PublishAsync(message));

        var received = new TaskCompletionSource<EmailVerificationRequested>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (await bus.SubscribeAsync<EmailVerificationRequested>("delivery-regression", (request, _) =>
        {
            received.TrySetResult(request);
            return Task.CompletedTask;
        }))
        {
            await bus.PublishAsync(message);
            Assert.Equal(message.EventId, (await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).EventId);
        }

        // With the consumer stopped, its durable queue must still accept delivery.
        await bus.PublishAsync(message);
    }
}
