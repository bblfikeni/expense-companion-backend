using Companion.Events;
using Companion.Notification;
using Companion.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Companion.Api.Tests;

public sealed class NotificationTemplateRendererTests
{
    private readonly NotificationTemplateRenderer _renderer = new(new TestHostEnvironment(AppContext.BaseDirectory));

    [Fact]
    public void RenderEmailVerification_EscapesHtmlAndFormatsExpirationInUtc()
    {
        var result = _renderer.Render(new EmailVerificationRequested
        {
            Email = "person@example.com",
            OtpCode = "<123456>",
            ExpiresAtUtc = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(2)),
        });

        Assert.Equal("person@example.com", result.Recipient);
        Assert.Equal("Verify your email address", result.Subject);
        Assert.Contains(">&lt;123456&gt;</td>", result.HtmlBody);
        Assert.Contains(">2026-11-05 12:30 UTC</p>", result.HtmlBody);
        Assert.Contains("<123456>", result.TextBody);
        Assert.Contains("2026-11-05 12:30 UTC", result.TextBody);
        AssertReceiptTheme(result);
    }

    [Fact]
    public void RenderPasswordReset_UsesPasswordResetSubject()
    {
        var result = _renderer.Render(new PasswordResetRequested
        {
            Email = "person@example.com",
            OtpCode = "123456",
            ExpiresAtUtc = DateTimeOffset.UtcNow,
        });

        Assert.Equal("Reset your password", result.Subject);
        Assert.Contains(">Password reset code</p>", result.HtmlBody);
        Assert.Contains(">123456</td>", result.HtmlBody);
        Assert.Contains("PASSWORD RESET CODE", result.TextBody);
        Assert.Contains("123456", result.TextBody);
        AssertReceiptTheme(result);
    }

    [Fact]
    public async Task Worker_WithoutSender_DoesNotSubscribeOrConsume()
    {
        var bus = new RecordingBus();
        using var worker = new NotificationWorker(NullLogger<NotificationWorker>.Instance, bus, _renderer, [], TimeProvider.System);
        await worker.StartAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, bus.SubscriptionCount);
        await worker.StopAsync(default);
    }

    [Fact]
    public async Task Worker_AwaitsProviderAcceptance_AndPropagatesDeliveryFailures()
    {
        var bus = new RecordingBus();
        var sender = new BlockingSender();
        using var worker = new NotificationWorker(NullLogger<NotificationWorker>.Instance, bus, _renderer, [sender], TimeProvider.System);
        await worker.StartAsync(default);
        try
        {
            var handler = await bus.Handler.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var delivery = handler(new EmailVerificationRequested
            {
                Email = "person@example.com", OtpCode = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            }, default);
            await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(delivery.IsCompleted);
            sender.Acceptance.SetException(new IOException("Injected provider failure"));
            await Assert.ThrowsAsync<IOException>(() => delivery);
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task Worker_ExpiredCode_SkipsDelivery()
    {
        var bus = new RecordingBus();
        var sender = new BlockingSender();
        using var worker = new NotificationWorker(NullLogger<NotificationWorker>.Instance, bus, _renderer, [sender], TimeProvider.System);
        await worker.StartAsync(default);
        try
        {
            var handler = await bus.Handler.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await handler(new EmailVerificationRequested
            {
                Email = "person@example.com",
                OtpCode = "123456",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            }, default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(sender.Started.Task.IsCompleted);
        }
        finally { await worker.StopAsync(default); }
    }

    private sealed class BlockingSender : INotificationSender
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Acceptance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendAsync(Guid eventId, RenderedNotification notification, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return Acceptance.Task;
        }
    }

    private sealed class RecordingBus : ICompanionMessageBus
    {
        public int SubscriptionCount { get; private set; }
        public TaskCompletionSource<Func<EmailVerificationRequested, CancellationToken, Task>> Handler { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => throw new NotSupportedException();
        public Task<IAsyncDisposable> SubscribeAsync<T>(string subscriptionId,
            Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class
        {
            SubscriptionCount++;
            if (typeof(T) == typeof(EmailVerificationRequested))
                Handler.TrySetResult((Func<EmailVerificationRequested, CancellationToken, Task>)(object)handler);
            return Task.FromResult<IAsyncDisposable>(new Subscription());
        }
        private sealed class Subscription : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static void AssertReceiptTheme(RenderedNotification result)
    {
        Assert.Contains("class=\"email-chrome\" bgcolor=\"#1F2328\"", result.HtmlBody);
        Assert.Contains("class=\"email-code\" bgcolor=\"#F2D644\"", result.HtmlBody);
        Assert.Contains("class=\"email-slip-edge\"", result.HtmlBody);
        Assert.Contains("@media (prefers-color-scheme: dark)", result.HtmlBody);
        Assert.DoesNotContain("{{", result.HtmlBody);
        Assert.DoesNotContain("{{", result.TextBody);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "Companion.Notification.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
