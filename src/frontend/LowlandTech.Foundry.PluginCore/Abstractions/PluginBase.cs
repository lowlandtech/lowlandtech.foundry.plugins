using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Abstract base class for plugins providing common functionality and lifecycle management.
/// </summary>
public abstract class PluginBase : IPlugin
{
    private PluginState _state = PluginState.Discovered;
    private List<IPluginFeature>? _features;

    /// <inheritdoc />
    public abstract PluginMetadata Metadata { get; }

    /// <inheritdoc />
    public PluginState State => _state;

    /// <inheritdoc />
    public IReadOnlyList<IPluginFeature> Features => _features ??= CreateFeatures().ToList();

    /// <summary>
    /// Override this method to create and return the features provided by this plugin.
    /// Features are created lazily on first access.
    /// </summary>
    /// <returns>An enumerable of plugin features.</returns>
    protected abstract IEnumerable<IPluginFeature> CreateFeatures();

    /// <inheritdoc />
    public virtual Task InstallAsync(IServiceProvider services)
    {
        // Override to perform installation tasks (e.g., database migrations)
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task UninstallAsync(IServiceProvider services)
    {
        // Override to perform cleanup tasks
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void Register(IServiceCollection services)
    {
        // Register the plugin itself
        services.AddSingleton<IPlugin>(this);

        // Let each feature register its own services
        foreach (var feature in Features)
        {
            feature.Register(services);
        }
    }

    /// <inheritdoc />
    public virtual Task ActivateAsync(IServiceProvider services)
    {
        _state = PluginState.Activated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeactivateAsync(IServiceProvider services)
    {
        _state = PluginState.Disabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetState(PluginState state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public void SetFeatureEnabled(string featureId, bool enabled)
    {
        var feature = Features.FirstOrDefault(f => f.Id == featureId);
        if (feature is PluginFeatureBase baseFeature)
        {
            baseFeature.SetEnabled(enabled);
        }
    }

    /// <summary>
    /// Gets a feature by its ID.
    /// </summary>
    /// <param name="featureId">The ID of the feature to find.</param>
    /// <returns>The feature if found, null otherwise.</returns>
    public IPluginFeature? GetFeature(string featureId)
        => Features.FirstOrDefault(f => f.Id == featureId);

    /// <summary>
    /// Gets a feature by its ID and type.
    /// </summary>
    /// <typeparam name="T">The type of feature to find.</typeparam>
    /// <param name="featureId">The ID of the feature to find.</param>
    /// <returns>The feature if found and of the correct type, null otherwise.</returns>
    public T? GetFeature<T>(string featureId) where T : class, IPluginFeature
        => Features.FirstOrDefault(f => f.Id == featureId) as T;

    /// <summary>
    /// Gets all features of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of features to find.</typeparam>
    /// <returns>All features of the specified type.</returns>
    public IEnumerable<T> GetFeatures<T>() where T : class, IPluginFeature
        => Features.OfType<T>();
}
