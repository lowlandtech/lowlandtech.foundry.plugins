namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Represents a single Plugin Catalog. Can contain 0-n plugins.
/// </summary>
public interface IPluginCatalog
{
    /// <summary>
    /// Initializes the catalog
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Gets if the catalog is initialized
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets all the plugins
    /// </summary>
    IReadOnlyList<PluginInfo> GetPlugins();

    /// <summary>
    /// Gets a single plugin based on its name and version
    /// </summary>
    PluginInfo? Get(string name, Version? version = null);
}
