using System.Net;
using System.Text.Json;
using Companion.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Companion.Api.Tests;

public sealed class NotificationEmailTests
{
    private const string From = "SARS Expense Companion <auth@example.com>";
    private const string ApiKey = "test-email-key";

    [Fact]
    public async Task SendAsync_ConfiguredSender_PostsBothTemplatesWithEventId()
    {
        var eventId = Guid.NewGuid();
        var handler = new StubEmailHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://api.resend.com/emails"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(ApiKey, request.Headers.Authorization?.Parameter);
            Assert.Equal(eventId.ToString("D"), Assert.Single(request.Headers.GetValues("Idempotency-Key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(From, body.RootElement.GetProperty("from").GetString());
            Assert.Equal("person@example.com", body.RootElement.GetProperty("to")[0].GetString());
            Assert.Equal("Verify your email address", body.RootElement.GetProperty("subject").GetString());
            Assert.Equal("<p>123456</p>", body.RootElement.GetProperty("html").GetString());
            Assert.Equal("123456", body.RootElement.GetProperty("text").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var host = BuildHost(handler);
        await host.StartAsync();
        await host.Services.GetRequiredService<INotificationSender>().SendAsync(eventId,
            new RenderedNotification("person@example.com", "Verify your email address", "<p>123456</p>", "123456"));
        await host.StopAsync();
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ThrowsWithoutLeakingResponseBody()
    {
        var handler = new StubEmailHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("private provider detail") }));
        using var host = BuildHost(handler);
        var sender = host.Services.GetRequiredService<INotificationSender>();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(Guid.NewGuid(),
            new RenderedNotification("person@example.com", "Reset your password", "<p>123456</p>", "123456")));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.DoesNotContain("private provider detail", error.Message);
    }

    [Fact]
    public async Task StartAsync_EmailEnabledWithoutCredentials_FailsValidation()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Channels:Email:Enabled"] = "true",
            ["Resend:ApiKey"] = "",
            ["Resend:From"] = "",
        });
        builder.Services.AddNotificationEmail(builder.Configuration);
        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    private static IHost BuildHost(HttpMessageHandler handler)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Channels:Email:Enabled"] = "true",
            ["Resend:ApiKey"] = ApiKey,
            ["Resend:From"] = From,
        });
        builder.Services.AddNotificationEmail(builder.Configuration);
        builder.Services.AddHttpClient(nameof(INotificationSender)).ConfigurePrimaryHttpMessageHandler(() => handler);
        return builder.Build();
    }

    private sealed class StubEmailHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
