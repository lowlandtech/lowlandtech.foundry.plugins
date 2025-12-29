namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Manages the lifecycle of all plugins including discovery, installation, activation, and feature management.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Gets all discovered plugins.
    /// </summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>
    /// Event raised when a plugin's state changes.
    /// </summary>
    event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    /// <summary>
    /// Event raised when a feature's enabled state changes.
    /// </summary>
    event EventHandler<FeatureStateChangedEventArgs>? FeatureStateChanged;

    /// <summary>
    /// Discovers all available plugins from configured sources.
    /// </summary>
    Task DiscoverPluginsAsync();

    /// <summary>
    /// Gets a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <returns>The plugin if found, null otherwise.</returns>
    IPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Installs a plugin, making it available for activation.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to install.</param>
    Task InstallPluginAsync(string pluginId);

    /// <summary>
    /// Uninstalls a plugin, removing it and cleaning up resources.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to uninstall.</param>
    Task UninstallPluginAsync(string pluginId);

    /// <summary>
    /// Activates a previously installed plugin.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to activate.</param>
    Task ActivatePluginAsync(string pluginId);

    /// <summary>
    /// Deactivates an active plugin without uninstalling it.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to deactivate.</param>
    Task DeactivatePluginAsync(string pluginId);

    /// <summary>
    /// Enables a specific feature within a plugin.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="featureId">The unique identifier of the feature within the plugin.</param>
    Task EnableFeatureAsync(string pluginId, string featureId);

    /// <summary>
    /// Disables a specific feature within a plugin.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="featureId">The unique identifier of the feature within the plugin.</param>
    Task DisableFeatureAsync(string pluginId, string featureId);
}

/// <summary>
/// Event arguments for plugin state changes.
/// </summary>
public class PluginStateChangedEventArgs : EventArgs
{
    public required string PluginId { get; init; }
    public required PluginState OldState { get; init; }
    public required PluginState NewState { get; init; }
}

/// <summary>
/// Event arguments for feature state changes.
/// </summary>
public class FeatureStateChangedEventArgs : EventArgs
{
    public required string PluginId { get; init; }
    public required string FeatureId { get; init; }
    public required bool IsEnabled { get; init; }
}
