using Companion.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Companion.Api.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"companion-config-{Guid.NewGuid():N}");
    private readonly PhysicalFileProvider _files;

    public ConfigurationTests()
    {
        Directory.CreateDirectory(_contentRoot);
        _files = new PhysicalFileProvider(_contentRoot);
        File.WriteAllText(Path.Combine(_contentRoot, "appsettings.json"),
            """{"Layered":"base","BaseOnly":"fallback","EnvWins":"base","CliWins":"base"}""");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void EnvironmentFile_OverridesDefaults_WhilePreservingOtherDefaultValues(string environmentName)
    {
        File.WriteAllText(Path.Combine(_contentRoot, $"appsettings.{environmentName}.json"),
            """{"Layered":"environment"}""");
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddCompanionAppSettings(EnvironmentFor(environmentName)).Build();

        Assert.Equal("environment", configuration["Layered"]);
        Assert.Equal("fallback", configuration["BaseOnly"]);
    }

    [Fact]
    public void MissingEnvironmentFile_IsOptional()
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddCompanionAppSettings(EnvironmentFor("Development")).Build();
        Assert.Equal("base", configuration["Layered"]);
    }

    [Fact]
    public void MissingDefaultFile_FailsWithTheMissingFilename()
    {
        File.Delete(Path.Combine(_contentRoot, "appsettings.json"));
        var exception = Assert.Throws<FileNotFoundException>(() => new ConfigurationBuilder()
            .AddCompanionAppSettings(EnvironmentFor("Development")).Build());
        Assert.Contains("appsettings.json", exception.Message);
    }

    [Fact]
    public void EnvironmentVariablesAndCommandLine_OverrideJson_EvenWhenRegisteredBeforeTheExtension()
    {
        File.WriteAllText(Path.Combine(_contentRoot, "appsettings.Development.json"),
            """{"EnvWins":"environment-file","CliWins":"environment-file"}""");
        var prefix = $"COMPANION_CONFIG_TEST_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable(prefix + "EnvWins", "environment-variable");
        Environment.SetEnvironmentVariable(prefix + "CliWins", "environment-variable");
        try
        {
            using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix)
                .AddCommandLine(["--CliWins=command-line"])
                .AddCompanionAppSettings(EnvironmentFor("Development")).Build();
            Assert.Equal("environment-variable", configuration["EnvWins"]);
            Assert.Equal("command-line", configuration["CliWins"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "EnvWins", null);
            Environment.SetEnvironmentVariable(prefix + "CliWins", null);
        }
    }

    [Fact]
    public void HostDefaults_AreReused_WithoutDuplicatingSourcesOrOverridingHostSettings()
    {
        File.WriteAllText(Path.Combine(_contentRoot, "appsettings.Development.json"),
            """{"Layered":"environment"}""");
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = _contentRoot,
            EnvironmentName = "Development",
            Args = ["--Layered=command-line"],
        });
        using var configuration = builder.Configuration;
        var count = configuration.Sources.Count;
        configuration.AddCompanionAppSettings(builder.Environment);
        configuration.AddCompanionAppSettings(builder.Environment);

        Assert.Equal(count, configuration.Sources.Count);
        Assert.Equal("command-line", configuration["Layered"]);
    }

    private IHostEnvironment EnvironmentFor(string environmentName) => new TestEnvironment
    {
        EnvironmentName = environmentName,
        ContentRootPath = _contentRoot,
        ContentRootFileProvider = _files,
    };

    public void Dispose()
    {
        _files.Dispose();
        Directory.Delete(_contentRoot, recursive: true);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ConfigurationTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
