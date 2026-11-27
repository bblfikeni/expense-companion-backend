using System.Text.RegularExpressions;
using Companion.Data;
using Companion.Extraction;
using Companion.Extraction.Configuration;
using Companion.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ExtractionProgram = Companion.Extraction.Program;

namespace Companion.Api.Tests;

public sealed class ExtractionHostTests
{
    [Fact]
    public async Task RealDefaultsStartAHostWithoutDatabaseBrokerOrWorkers()
    {
        var builder = CreateBuilder();
        ExtractionProgram.Configure(builder);
        using var host = builder.Build();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(watchdog.Token);
        Assert.False(host.Services.GetRequiredService<IOptions<ExtractionOptions>>().Value.Enabled);
        Assert.Null(host.Services.GetService<IDbContextFactory<CompanionDbContext>>());
        Assert.Null(host.Services.GetService<ICompanionMessageBus>());
        Assert.Empty(host.Services.GetServices<IHostedService>());
        await host.StopAsync(watchdog.Token);
    }

    [Fact]
    public void LocalOptInRegistersBothWorkersAndRequiresExtractionConnectionString()
    {
        var builder = CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Extraction:Enabled"] = "true",
            ["ConnectionStrings:CompanionDb"] = "Host=localhost;Database=wrong",
            ["ConnectionStrings:ExtractionDb"] = "",
        });
        var failure = Assert.Throws<InvalidOperationException>(() => ExtractionProgram.Configure(builder));
        Assert.Contains("ConnectionStrings__ExtractionDb", failure.Message);

        builder = CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Extraction:Enabled"] = "true",
            ["ConnectionStrings:ExtractionDb"] = "Host=localhost;Database=extraction",
            ["RabbitMq:ConnectionString"] = "host=localhost;username=unused;password=unused",
        });
        ExtractionProgram.Configure(builder);
        using var host = builder.Build();
        var workers = host.Services.GetServices<IHostedService>().ToArray();
        Assert.Contains(workers, service => service is ExtractionWorker);
        Assert.Contains(workers, service => service is ReceiptRecoveryWorker);
    }

    [Fact]
    public void RepositoryAppSettingsAndComposeKeepExtractionOffByDefault()
    {
        var root = FindRepository();
        var extractionSettings = Path.Combine(root, "Services", "Companion.Extraction", "appsettings.json");
        var defaultSettings = new ConfigurationBuilder().AddJsonFile(extractionSettings).Build();
        Assert.False(defaultSettings.GetValue<bool?>("Extraction:Enabled"));
        foreach (var file in Directory.EnumerateFiles(root, "appsettings*.json")
                     .Concat(Directory.EnumerateDirectories(Path.Combine(root, "Services"))
                         .SelectMany(directory => Directory.EnumerateFiles(directory, "appsettings*.json"))))
        {
            var configuration = new ConfigurationBuilder().AddJsonFile(file).Build();
            Assert.True(!configuration.GetValue("Extraction:Enabled", false), Path.GetRelativePath(root, file));
        }

        var composeFiles = Directory.EnumerateFiles(root, "*compose*.y*ml").ToArray();
        Assert.Contains(Path.Combine(root, "docker-compose.yml"), composeFiles);
        foreach (var file in composeFiles)
        {
            var values = Regex.Matches(File.ReadAllText(file),
                """(?m)^\s*(?:-\s*)?Extraction__Enabled\s*[:=]\s*([^#\r\n]+)""")
                .Select(match => match.Groups[1].Value.Trim().Trim('\'', '"'));
            if (Path.GetFileName(file) == "docker-compose.yml")
                Assert.Equal("false", Assert.Single(values));
            else
                Assert.All(values, value => Assert.Equal("false", value));
        }
    }

    private static HostApplicationBuilder CreateBuilder() => Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        ContentRootPath = Path.Combine(FindRepository(), "Services", "Companion.Extraction"),
        EnvironmentName = Environments.Production,
    });

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SarsCompanion.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Companion repository root was not found.");
    }
}
