using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace Companion.Configuration;

public static class CompanionConfigurationBuilderExtensions
{
    /// <summary>
    /// Loads appsettings.json and optional appsettings.{EnvironmentName}.json
    /// from the host's content root. Existing host defaults are reused; later
    /// providers such as user secrets, environment variables and command-line
    /// arguments retain their precedence over these files.
    /// </summary>
    public static IConfigurationBuilder AddCompanionAppSettings(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment.EnvironmentName);

        var defaults = FindJsonSource(configuration, "appsettings.json");
        if (defaults is null)
        {
            defaults = CreateJsonSource(environment, "appsettings.json", optional: false);
            configuration.Sources.Insert(0, defaults);
        }

        var environmentFile = $"appsettings.{environment.EnvironmentName}.json";
        if (FindJsonSource(configuration, environmentFile) is null)
        {
            configuration.Sources.Insert(configuration.Sources.IndexOf(defaults) + 1,
                CreateJsonSource(environment, environmentFile, optional: true));
        }

        return configuration;
    }

    private static JsonConfigurationSource? FindJsonSource(IConfigurationBuilder configuration, string path)
        => configuration.Sources.OfType<JsonConfigurationSource>()
            .FirstOrDefault(source => string.Equals(source.Path, path, StringComparison.Ordinal));

    private static JsonConfigurationSource CreateJsonSource(IHostEnvironment environment, string path, bool optional)
        => new()
        {
            FileProvider = environment.ContentRootFileProvider,
            Path = path,
            Optional = optional,
            ReloadOnChange = true,
        };
}
