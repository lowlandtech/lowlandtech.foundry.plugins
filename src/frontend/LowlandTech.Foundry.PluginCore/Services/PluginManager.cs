using System.Reflection;
using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Catalogs;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Services;

/// <summary>
/// Manages the lifecycle of all plugins including discovery, installation, activation, and feature management.
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly IPluginCatalog? _catalog;
    private readonly IPluginStateStore _stateStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager>? _logger;
    private readonly List<Assembly> _additionalAssemblies;
    private readonly List<IPlugin> _plugins = [];
    private bool _isInitialized;

    /// <inheritdoc />
    public event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    /// <inheritdoc />
    public event EventHandler<FeatureStateChangedEventArgs>? FeatureStateChanged;

    /// <summary>
    /// Creates a new plugin manager.
    /// </summary>
    /// <param name="stateStore">The state store for persisting plugin state.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="catalog">Optional plugin catalog for assembly-based discovery.</param>
    /// <param name="additionalAssemblies">Additional assemblies to scan for plugins.</param>
    /// <param name="logger">Optional logger.</param>
    public PluginManager(
        IPluginStateStore stateStore,
        IServiceProvider serviceProvider,
        IPluginCatalog? catalog = null,
        IEnumerable<Assembly>? additionalAssemblies = null,
        ILogger<PluginManager>? logger = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _catalog = catalog;
        _additionalAssemblies = additionalAssemblies?.ToList() ?? [];
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<IPlugin> Plugins => _plugins;

    /// <inheritdoc />
    public async Task DiscoverPluginsAsync()
    {
        if (_isInitialized)
        {
            _logger?.LogWarning("PluginManager already initialized, skipping discovery");
            return;
        }

        _logger?.LogInformation("Discovering plugins...");

        // Discover plugins from catalog
        if (_catalog != null)
        {
            if (!_catalog.IsInitialized)
            {
                await _catalog.InitializeAsync();
            }

            foreach (var pluginInfo in _catalog.GetPlugins())
            {
                await TryLoadPluginFromType(pluginInfo.Type);
            }
        }

        // Discover plugins from additional assemblies
        foreach (var assembly in _additionalAssemblies)
        {
            DiscoverPluginsFromAssembly(assembly);
        }

        // Restore persisted states
        await RestorePluginStatesAsync();

        _isInitialized = true;
        _logger?.LogInformation("Discovered {Count} plugins", _plugins.Count);
    }

    private void DiscoverPluginsFromAssembly(Assembly assembly)
    {
        try
        {
            var pluginTypes = assembly.GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IPlugin).IsAssignableFrom(t));

            foreach (var type in pluginTypes)
            {
                TryLoadPluginFromType(type).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to discover plugins from assembly {Assembly}", assembly.FullName);
        }
    }

    private async Task TryLoadPluginFromType(Type type)
    {
        try
        {
            if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            {
                return;
            }

            // Check if already loaded
            var existing = _plugins.FirstOrDefault(p => p.GetType() == type);
            if (existing != null)
            {
                return;
            }

            // Create plugin instance
            var plugin = (IPlugin?)Activator.CreateInstance(type);
            if (plugin == null)
            {
                _logger?.LogWarning("Failed to create instance of plugin type {Type}", type.FullName);
                return;
            }

            _plugins.Add(plugin);
            _logger?.LogInformation("Discovered plugin: {PluginId} v{Version}",
                plugin.Metadata.Id, plugin.Metadata.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load plugin from type {Type}", type.FullName);
        }

        await Task.CompletedTask;
    }

    private async Task RestorePluginStatesAsync()
    {
        var savedStates = await _stateStore.GetAllPluginStatesAsync();

        foreach (var plugin in _plugins)
        {
            var savedState = savedStates.FirstOrDefault(s => s.PluginId == plugin.Metadata.Id);
            if (savedState != null)
            {
                plugin.SetState(savedState.State);

                // Restore feature states
                var featureStates = await _stateStore.GetFeatureStatesAsync(plugin.Metadata.Id);
                foreach (var featureState in featureStates)
                {
                    plugin.SetFeatureEnabled(featureState.FeatureId, featureState.IsEnabled);
                }
            }
        }
    }

    /// <inheritdoc />
    public IPlugin? GetPlugin(string pluginId)
        => _plugins.FirstOrDefault(p => p.Metadata.Id == pluginId);

    /// <inheritdoc />
    public async Task InstallPluginAsync(string pluginId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        if (plugin.State != PluginState.Discovered)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' is already installed (state: {plugin.State})");
        }

        _logger?.LogInformation("Installing plugin: {PluginId}", pluginId);

        try
        {
            await plugin.InstallAsync(_serviceProvider);

            var oldState = plugin.State;
            plugin.SetState(PluginState.Installed);

            await _stateStore.SavePluginStateAsync(new PluginStateRecord
            {
                PluginId = pluginId,
                State = PluginState.Installed,
                InstalledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            OnPluginStateChanged(pluginId, oldState, PluginState.Installed);
            _logger?.LogInformation("Plugin installed: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to install plugin: {PluginId}", pluginId);
            plugin.SetState(PluginState.Error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UninstallPluginAsync(string pluginId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        if (plugin.State == PluginState.Discovered)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' is not installed");
        }

        _logger?.LogInformation("Uninstalling plugin: {PluginId}", pluginId);

        try
        {
            // Deactivate first if active
            if (plugin.State == PluginState.Activated)
            {
                await plugin.DeactivateAsync(_serviceProvider);
            }

            await plugin.UninstallAsync(_serviceProvider);

            var oldState = plugin.State;
            plugin.SetState(PluginState.Discovered);

            await _stateStore.DeletePluginStateAsync(pluginId);
            await _stateStore.DeleteFeatureStatesAsync(pluginId);

            OnPluginStateChanged(pluginId, oldState, PluginState.Discovered);
            _logger?.LogInformation("Plugin uninstalled: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to uninstall plugin: {PluginId}", pluginId);
            plugin.SetState(PluginState.Error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ActivatePluginAsync(string pluginId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        if (plugin.State is not (PluginState.Installed or PluginState.Disabled))
        {
            throw new InvalidOperationException(
                $"Plugin '{pluginId}' cannot be activated (state: {plugin.State})");
        }

        _logger?.LogInformation("Activating plugin: {PluginId}", pluginId);

        try
        {
            await plugin.ActivateAsync(_serviceProvider);

            var oldState = plugin.State;
            plugin.SetState(PluginState.Activated);

            await _stateStore.SavePluginStateAsync(new PluginStateRecord
            {
                PluginId = pluginId,
                State = PluginState.Activated,
                InstalledAt = (await _stateStore.GetPluginStateAsync(pluginId))?.InstalledAt ?? DateTime.UtcNow,
                ActivatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            OnPluginStateChanged(pluginId, oldState, PluginState.Activated);
            _logger?.LogInformation("Plugin activated: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to activate plugin: {PluginId}", pluginId);
            plugin.SetState(PluginState.Error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeactivatePluginAsync(string pluginId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        if (plugin.State != PluginState.Activated)
        {
            throw new InvalidOperationException(
                $"Plugin '{pluginId}' is not activated (state: {plugin.State})");
        }

        _logger?.LogInformation("Deactivating plugin: {PluginId}", pluginId);

        try
        {
            await plugin.DeactivateAsync(_serviceProvider);

            var oldState = plugin.State;
            plugin.SetState(PluginState.Disabled);

            await _stateStore.SavePluginStateAsync(new PluginStateRecord
            {
                PluginId = pluginId,
                State = PluginState.Disabled,
                InstalledAt = (await _stateStore.GetPluginStateAsync(pluginId))?.InstalledAt ?? DateTime.UtcNow,
                ActivatedAt = null,
                UpdatedAt = DateTime.UtcNow
            });

            OnPluginStateChanged(pluginId, oldState, PluginState.Disabled);
            _logger?.LogInformation("Plugin deactivated: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deactivate plugin: {PluginId}", pluginId);
            plugin.SetState(PluginState.Error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task EnableFeatureAsync(string pluginId, string featureId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        var feature = plugin.Features.FirstOrDefault(f => f.Id == featureId)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found in plugin '{pluginId}'");

        if (feature.IsEnabled)
        {
            return;
        }

        _logger?.LogInformation("Enabling feature: {PluginId}/{FeatureId}", pluginId, featureId);

        await feature.EnableAsync(_serviceProvider);
        plugin.SetFeatureEnabled(featureId, true);

        await _stateStore.SaveFeatureStateAsync(new FeatureStateRecord
        {
            PluginId = pluginId,
            FeatureId = featureId,
            IsEnabled = true,
            UpdatedAt = DateTime.UtcNow
        });

        OnFeatureStateChanged(pluginId, featureId, true);
    }

    /// <inheritdoc />
    public async Task DisableFeatureAsync(string pluginId, string featureId)
    {
        var plugin = GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found");

        var feature = plugin.Features.FirstOrDefault(f => f.Id == featureId)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found in plugin '{pluginId}'");

        if (!feature.IsEnabled)
        {
            return;
        }

        _logger?.LogInformation("Disabling feature: {PluginId}/{FeatureId}", pluginId, featureId);

        await feature.DisableAsync(_serviceProvider);
        plugin.SetFeatureEnabled(featureId, false);

        await _stateStore.SaveFeatureStateAsync(new FeatureStateRecord
        {
            PluginId = pluginId,
            FeatureId = featureId,
            IsEnabled = false,
            UpdatedAt = DateTime.UtcNow
        });

        OnFeatureStateChanged(pluginId, featureId, false);
    }

    private void OnPluginStateChanged(string pluginId, PluginState oldState, PluginState newState)
    {
        PluginStateChanged?.Invoke(this, new PluginStateChangedEventArgs
        {
            PluginId = pluginId,
            OldState = oldState,
            NewState = newState
        });
    }

    private void OnFeatureStateChanged(string pluginId, string featureId, bool isEnabled)
    {
        FeatureStateChanged?.Invoke(this, new FeatureStateChangedEventArgs
        {
            PluginId = pluginId,
            FeatureId = featureId,
            IsEnabled = isEnabled
        });
    }
}
