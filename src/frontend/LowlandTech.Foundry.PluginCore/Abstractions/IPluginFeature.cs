using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Represents a feature within a plugin that can be independently enabled or disabled.
/// Features self-register their services and can be queried for their capabilities.
/// </summary>
public interface IPluginFeature
{
    /// <summary>
    /// Gets the unique identifier for this feature within its plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable name of this feature.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this feature provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets whether this feature is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the plugin that owns this feature.
    /// </summary>
    IPlugin Plugin { get; }

    /// <summary>
    /// Registers this feature's services with the dependency injection container.
    /// Called during application startup. Should check IsEnabled before registering.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    void Register(IServiceCollection services);

    /// <summary>
    /// Enables this feature at runtime.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task EnableAsync(IServiceProvider services);

    /// <summary>
    /// Disables this feature at runtime.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task DisableAsync(IServiceProvider services);
}
