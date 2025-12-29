using LowlandTech.Foundry.Api.Services;
using LowlandTech.Foundry.PluginCore.Hosting;

namespace LowlandTech.Foundry.Api.Extensions;

/// <summary>
/// Extension methods for registering plugin hosting services.
/// </summary>
public static class PluginHostingExtensions
{
    /// <summary>
    /// Adds plugin hosting services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPluginHosting(this IServiceCollection services)
    {
        // Register the plugin hosts
        services.AddSingleton<ContainerPluginHost>();
        services.AddSingleton<InProcessPluginHost>();

        // Register the factory
        services.AddSingleton<IPluginHostFactory, PluginHostFactory>();

        // Add HTTP client factory for health checks
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Adds plugin hosting services with a specific default mode.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="defaultMode">The default hosting mode.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPluginHosting(
        this IServiceCollection services,
        string defaultMode)
    {
        services.AddPluginHosting();

        // Configure default mode via options if needed
        services.Configure<PluginHostingOptions>(options =>
        {
            options.DefaultHostingMode = defaultMode;
        });

        return services;
    }
}

/// <summary>
/// Options for plugin hosting configuration.
/// </summary>
public class PluginHostingOptions
{
    /// <summary>
    /// Default hosting mode for new plugins.
    /// </summary>
    public string DefaultHostingMode { get; set; } = "Container";

    /// <summary>
    /// Base path for plugin routes.
    /// </summary>
    public string PluginRouteBase { get; set; } = "/api/plugins";

    /// <summary>
    /// Docker network name for containers.
    /// </summary>
    public string DockerNetwork { get; set; } = "foundry-plugins";

    /// <summary>
    /// Whether to auto-activate plugins on startup.
    /// </summary>
    public bool AutoActivateOnStartup { get; set; } = true;
}
