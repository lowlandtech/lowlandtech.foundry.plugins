using LowlandTech.Foundry.PluginCore.Abstractions;

namespace LowlandTech.Foundry.PluginCore.Services;

/// <summary>
/// Persists plugin and feature state across application restarts.
/// </summary>
public interface IPluginStateStore
{
    /// <summary>
    /// Gets the persisted state for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin state record if found, null otherwise.</returns>
    Task<PluginStateRecord?> GetPluginStateAsync(string pluginId);

    /// <summary>
    /// Gets all persisted plugin states.
    /// </summary>
    /// <returns>All plugin state records.</returns>
    Task<IReadOnlyList<PluginStateRecord>> GetAllPluginStatesAsync();

    /// <summary>
    /// Saves the state for a plugin.
    /// </summary>
    /// <param name="record">The plugin state record to save.</param>
    Task SavePluginStateAsync(PluginStateRecord record);

    /// <summary>
    /// Deletes the state for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    Task DeletePluginStateAsync(string pluginId);

    /// <summary>
    /// Gets the enabled state for a feature.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="featureId">The feature ID.</param>
    /// <returns>The feature state record if found, null otherwise.</returns>
    Task<FeatureStateRecord?> GetFeatureStateAsync(string pluginId, string featureId);

    /// <summary>
    /// Gets all feature states for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>All feature state records for the plugin.</returns>
    Task<IReadOnlyList<FeatureStateRecord>> GetFeatureStatesAsync(string pluginId);

    /// <summary>
    /// Saves the state for a feature.
    /// </summary>
    /// <param name="record">The feature state record to save.</param>
    Task SaveFeatureStateAsync(FeatureStateRecord record);

    /// <summary>
    /// Deletes all feature states for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    Task DeleteFeatureStatesAsync(string pluginId);
}

/// <summary>
/// Represents the persisted state of a plugin.
/// </summary>
public record PluginStateRecord
{
    public required string PluginId { get; init; }
    public required PluginState State { get; init; }
    public DateTime InstalledAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents the persisted state of a feature.
/// </summary>
public record FeatureStateRecord
{
    public required string PluginId { get; init; }
    public required string FeatureId { get; init; }
    public required bool IsEnabled { get; init; }
    public DateTime UpdatedAt { get; init; }
}
