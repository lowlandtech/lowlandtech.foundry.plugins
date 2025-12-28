using LowlandTech.Foundry.PluginCore.Catalogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Services;

/// <summary>
/// Hosted service that initializes plugin catalogs at application startup
/// </summary>
public class PluginCatalogInitializer : IHostedService
{
    private readonly IPluginCatalog _catalog;
    private readonly ILogger<PluginCatalogInitializer> _logger;

    public PluginCatalogInitializer(IPluginCatalog catalog, ILogger<PluginCatalogInitializer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing plugin catalogs...");

        try
        {
            await _catalog.InitializeAsync();

            var plugins = _catalog.GetPlugins();
            _logger.LogInformation("Plugin initialization complete. Loaded {Count} plugins.", plugins.Count);

            foreach (var plugin in plugins)
            {
                _logger.LogDebug("  - {PluginName} v{Version} from {Assembly}",
                    plugin.Name, plugin.Version, plugin.Assembly.GetName().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin catalogs");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
