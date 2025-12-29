using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Abstract base class for plugin features providing common functionality.
/// </summary>
public abstract class PluginFeatureBase : IPluginFeature
{
    private bool _isEnabled = true;

    /// <summary>
    /// Creates a new plugin feature.
    /// </summary>
    /// <param name="plugin">The plugin that owns this feature.</param>
    /// <param name="id">Unique identifier for this feature.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Description of the feature.</param>
    protected PluginFeatureBase(IPlugin plugin, string id, string name, string description)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public bool IsEnabled => _isEnabled;

    /// <inheritdoc />
    public IPlugin Plugin { get; }

    /// <inheritdoc />
    public abstract void Register(IServiceCollection services);

    /// <inheritdoc />
    public virtual Task EnableAsync(IServiceProvider services)
    {
        _isEnabled = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DisableAsync(IServiceProvider services)
    {
        _isEnabled = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the enabled state directly. Used by the plugin manager.
    /// </summary>
    internal void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }
}
