using System.Collections.Concurrent;
using LowlandTech.Foundry.PluginCore.Abstractions;

namespace LowlandTech.Foundry.PluginCore.Services;

/// <summary>
/// In-memory implementation of plugin state store for testing or when no persistence is needed.
/// State is lost when the application restarts.
/// </summary>
public class InMemoryPluginStateStore : IPluginStateStore
{
    private readonly ConcurrentDictionary<string, PluginStateRecord> _pluginStates = new();
    private readonly ConcurrentDictionary<string, FeatureStateRecord> _featureStates = new();

    /// <inheritdoc />
    public Task<PluginStateRecord?> GetPluginStateAsync(string pluginId)
    {
        _pluginStates.TryGetValue(pluginId, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PluginStateRecord>> GetAllPluginStatesAsync()
    {
        return Task.FromResult<IReadOnlyList<PluginStateRecord>>(_pluginStates.Values.ToList());
    }

    /// <inheritdoc />
    public Task SavePluginStateAsync(PluginStateRecord record)
    {
        _pluginStates[record.PluginId] = record with { UpdatedAt = DateTime.UtcNow };
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeletePluginStateAsync(string pluginId)
    {
        _pluginStates.TryRemove(pluginId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<FeatureStateRecord?> GetFeatureStateAsync(string pluginId, string featureId)
    {
        var key = GetFeatureKey(pluginId, featureId);
        _featureStates.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureStateRecord>> GetFeatureStatesAsync(string pluginId)
    {
        var records = _featureStates.Values
            .Where(r => r.PluginId == pluginId)
            .ToList();
        return Task.FromResult<IReadOnlyList<FeatureStateRecord>>(records);
    }

    /// <inheritdoc />
    public Task SaveFeatureStateAsync(FeatureStateRecord record)
    {
        var key = GetFeatureKey(record.PluginId, record.FeatureId);
        _featureStates[key] = record with { UpdatedAt = DateTime.UtcNow };
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteFeatureStatesAsync(string pluginId)
    {
        var keysToRemove = _featureStates.Keys
            .Where(k => k.StartsWith(pluginId + ":"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _featureStates.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private static string GetFeatureKey(string pluginId, string featureId)
        => $"{pluginId}:{featureId}";
}
