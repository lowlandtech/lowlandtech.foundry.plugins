using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Represents a plugin with lifecycle management and features.
/// Plugins are discovered automatically and can be installed, activated, and disabled.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the metadata for this plugin including ID, name, version, and description.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Gets the current lifecycle state of this plugin.
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// Gets the features provided by this plugin.
    /// </summary>
    IReadOnlyList<IPluginFeature> Features { get; }

    /// <summary>
    /// Performs one-time installation of the plugin.
    /// Called when the plugin is first installed. Use for database migrations, file creation, etc.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task InstallAsync(IServiceProvider services);

    /// <summary>
    /// Performs cleanup when the plugin is uninstalled.
    /// Called when the plugin is being removed. Use for cleanup of resources created during install.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task UninstallAsync(IServiceProvider services);

    /// <summary>
    /// Registers services with the dependency injection container.
    /// Called during application startup. This is platform-agnostic (no IApplicationBuilder).
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    void Register(IServiceCollection services);

    /// <summary>
    /// Activates the plugin at runtime.
    /// Called after installation to start the plugin.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task ActivateAsync(IServiceProvider services);

    /// <summary>
    /// Deactivates the plugin at runtime.
    /// Called to stop the plugin without uninstalling it.
    /// </summary>
    /// <param name="services">The service provider for accessing dependencies.</param>
    Task DeactivateAsync(IServiceProvider services);

    /// <summary>
    /// Sets the state of the plugin. Used internally by the plugin manager.
    /// </summary>
    /// <param name="state">The new state to set.</param>
    void SetState(PluginState state);

    /// <summary>
    /// Sets the enabled state of a feature. Used internally by the plugin manager.
    /// </summary>
    /// <param name="featureId">The ID of the feature to update.</param>
    /// <param name="enabled">Whether the feature should be enabled.</param>
    void SetFeatureEnabled(string featureId, bool enabled);
}
