using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Companion.Messaging;
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
    private const string LocalPickupBroker = "host=localhost;virtualHost=companion-dev-mail";

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
        builder.Services.AddNotificationEmail(builder.Configuration, builder.Environment);
        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task SendAsync_DevelopmentPickup_WritesMailWithoutProviderCredentials()
    {
        var directory = Directory.CreateTempSubdirectory("companion-mail-");
        try
        {
            var pickup = Path.Combine(directory.FullName, "pickup");
            using var host = BuildPickupHost(pickup, LocalPickupBroker);
            await host.StartAsync();
            var eventId = Guid.NewGuid();
            await host.Services.GetRequiredService<INotificationSender>().SendAsync(eventId,
                new RenderedNotification("person@example.test", "Reset your password", "<p>123456</p>", "123456"));

            var mail = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(pickup, "*.eml")));
            Assert.Contains("person@example.test", mail);
            Assert.Contains("Subject: Reset your password", mail);
            Assert.Contains($"X-Companion-Event-Id: {eventId:D}", mail);
            Assert.Contains("123456", mail);
            await host.StopAsync();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("host=localhost;virtualHost=companion")]
    [InlineData("host=localhost")]
    [InlineData("host=broker.example.test;virtualHost=companion-dev-mail")]
    [InlineData("host=localhost,broker.example.test;virtualHost=companion-dev-mail")]
    public void ResolvePickupSender_SharedBrokerConfiguration_RejectsBroker(string connectionString)
    {
        using var host = BuildPickupHost("unused", connectionString);
        var error = Assert.Throws<InvalidOperationException>(
            () => host.Services.GetRequiredService<INotificationSender>());
        Assert.Contains("loopback RabbitMQ broker", error.Message);
    }

    [Fact]
    public void ResolveResendSender_PickupVirtualHost_RejectsBroker()
    {
        using var host = BuildHost(
            new StubEmailHandler((_, _) => throw new InvalidOperationException("Unexpected email send.")),
            LocalPickupBroker);
        var error = Assert.Throws<InvalidOperationException>(
            () => host.Services.GetRequiredService<INotificationSender>());
        Assert.Contains("reserved for development mail pickup", error.Message);
    }

    [Fact]
    public async Task SendAsync_PickupDirectoryAccessibleToOtherUsers_RejectsDirectory()
    {
        if (OperatingSystem.IsWindows())
            return;

        var directory = Directory.CreateTempSubdirectory("companion-mail-");
        try
        {
            var privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(directory.FullName, privateMode | UnixFileMode.GroupExecute);
            using (var invalidHost = BuildPickupHost(directory.FullName, LocalPickupBroker))
            {
                var error = Assert.Throws<InvalidOperationException>(
                    () => invalidHost.Services.GetRequiredService<INotificationSender>());
                Assert.Contains("private directory", error.Message);
            }

            File.SetUnixFileMode(directory.FullName, privateMode);
            using var host = BuildPickupHost(directory.FullName, LocalPickupBroker);
            var sender = host.Services.GetRequiredService<INotificationSender>();
            File.SetUnixFileMode(directory.FullName, privateMode | UnixFileMode.OtherExecute);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Guid.NewGuid(),
                new RenderedNotification("person@example.test", "Reset your password", "<p>123456</p>", "123456")));
            Assert.Empty(directory.GetFiles("*.eml"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ResolvePickupSender_MacDirectoryWithExtendedAcl_RejectsDirectory()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var directory = Directory.CreateTempSubdirectory("companion-mail-");
        try
        {
            var chmod = new ProcessStartInfo("/bin/chmod")
            {
                ArgumentList = { "+a", "everyone allow list,search", directory.FullName },
            };
            using var process = Process.Start(chmod)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);

            using var host = BuildPickupHost(directory.FullName, LocalPickupBroker);
            var error = Assert.Throws<InvalidOperationException>(
                () => host.Services.GetRequiredService<INotificationSender>());
            Assert.Contains("private directory", error.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AddNotificationEmail_ProductionPickup_RejectsConfiguration()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration["Channels:Email:PickupDirectory"] = "secrets/dev-mail";
        Assert.Throws<InvalidOperationException>(
            () => builder.Services.AddNotificationEmail(builder.Configuration, builder.Environment));
    }

    private static IHost BuildPickupHost(string directory, string connectionString)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Channels:Email:PickupDirectory"] = directory,
            ["RabbitMq:ConnectionString"] = connectionString,
            ["Resend:ApiKey"] = "",
        });
        builder.Services.AddCompanionMessaging(builder.Configuration);
        builder.Services.AddNotificationEmail(builder.Configuration, builder.Environment);
        return builder.Build();
    }

    private static IHost BuildHost(HttpMessageHandler handler,
        string connectionString = "host=localhost;virtualHost=companion")
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Channels:Email:Enabled"] = "true",
            ["Resend:ApiKey"] = ApiKey,
            ["Resend:From"] = From,
            ["RabbitMq:ConnectionString"] = connectionString,
        });
        builder.Services.AddCompanionMessaging(builder.Configuration);
        builder.Services.AddNotificationEmail(builder.Configuration, builder.Environment);
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
