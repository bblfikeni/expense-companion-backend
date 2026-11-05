using System.Text.Json;
using Companion.Api.Services;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Models;
using Companion.Data.Extensions;
using Companion.Events;
using Companion.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class OutboxPublisherTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("UnknownEvent", "{}")]
    [InlineData(nameof(EmailVerificationRequested), "null")]
    public async Task PoisonMessage_IsQuarantined_AndFollowingMessagePublishes(string type, string payload)
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        await SeedAsync(host.Services,
            new OutboxMessage { Type = type, Payload = payload }, ValidMessage("good@example.com"));
        var bus = new RecordingBus();
        using var publisher = CreatePublisher(host.Services, bus);
        await publisher.StartAsync(default);
        try
        {
            await WaitUntilAsync(host.Services, rows => rows[1].PublishedAt is not null);
            var rows = await ReadRowsAsync(host.Services);
            Assert.NotNull(rows[0].FailedAt);
            Assert.Equal(1, rows[0].Attempts);
            Assert.Null(rows[0].PublishedAt);
            Assert.Equal(0, rows[1].Attempts);
        }
        finally { await publisher.StopAsync(default); }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, true)]
    public async Task BrokerFailure_RetainsMessage_WithoutBlockingFollowingRows(int previousAttempts, bool quarantined)
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var failing = ValidMessage("fail@example.com");
        failing.Attempts = previousAttempts;
        await SeedAsync(host.Services, failing, ValidMessage("good@example.com"));
        using var publisher = CreatePublisher(host.Services, new RecordingBus());
        await publisher.StartAsync(default);
        try
        {
            await WaitUntilAsync(host.Services, rows => rows[1].PublishedAt is not null);
            var failed = (await ReadRowsAsync(host.Services))[0];
            Assert.Null(failed.PublishedAt);
            Assert.Equal(previousAttempts + 1, failed.Attempts);
            Assert.Equal(quarantined, failed.FailedAt is not null);
            if (!quarantined)
                Assert.NotNull(failed.NextAttemptAt);
        }
        finally { await publisher.StopAsync(default); }
    }

    [Fact]
    public async Task ScheduledMessage_IsSkippedUntilDue_ThenPublishes()
    {
        using var host = CompanionHosts.Build(await postgres.CreateEmptyDatabaseAsync());
        await host.ApplyCompanionMigrationsAsync();
        var clock = new AdjustableClock();
        var scheduled = ValidMessage("scheduled@example.com");
        scheduled.NextAttemptAt = clock.GetUtcNow().AddHours(1);
        await SeedAsync(host.Services, scheduled, ValidMessage("good@example.com"));
        using var publisher = new OutboxPublisher(host.Services.GetRequiredService<IServiceScopeFactory>(),
            new RecordingBus(), new CompanionMessagingOptions { OutboxPollingIntervalMilliseconds = 10 },
            clock, NullLogger<OutboxPublisher>.Instance);
        await publisher.StartAsync(default);
        try
        {
            await WaitUntilAsync(host.Services, rows => rows[1].PublishedAt is not null);
            Assert.Null((await ReadRowsAsync(host.Services))[0].PublishedAt);
            clock.Advance(TimeSpan.FromHours(2));
            await WaitUntilAsync(host.Services, rows => rows[0].PublishedAt is not null);
        }
        finally { await publisher.StopAsync(default); }
    }

    private sealed class AdjustableClock : TimeProvider
    {
        private long _utcTicks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _utcTicks, amount.Ticks);
    }

    private static OutboxPublisher CreatePublisher(IServiceProvider services, ICompanionMessageBus bus)
        => new(services.GetRequiredService<IServiceScopeFactory>(), bus,
            new CompanionMessagingOptions { OutboxPollingIntervalMilliseconds = 10 },
            TimeProvider.System, NullLogger<OutboxPublisher>.Instance);

    private static OutboxMessage ValidMessage(string email) => new()
    {
        Type = nameof(EmailVerificationRequested),
        Payload = JsonSerializer.Serialize(new EmailVerificationRequested { Email = email, OtpCode = "123456" }),
    };

    private static async Task SeedAsync(IServiceProvider services, params OutboxMessage[] messages)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        foreach (var message in messages)
        {
            message.CreatedAt = DateTimeOffset.UtcNow;
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<List<OutboxMessage>> ReadRowsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CompanionDbContext>()
            .OutboxMessages.AsNoTracking().OrderBy(row => row.Id).ToListAsync();
    }

    private static async Task WaitUntilAsync(IServiceProvider services, Func<List<OutboxMessage>, bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition(await ReadRowsAsync(services)))
            await Task.Delay(10, timeout.Token);
    }

    private sealed class RecordingBus : ICompanionMessageBus
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => message is EmailVerificationRequested { Email: "fail@example.com" }
                ? Task.FromException(new IOException("Injected broker failure")) : Task.CompletedTask;

        public Task<IAsyncDisposable> SubscribeAsync<T>(string subscriptionId,
            Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class
            => throw new NotSupportedException();
    }
}
