namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Plugin catalog that combines multiple catalogs
/// </summary>
public class CompositePluginCatalog : IPluginCatalog
{
    private readonly List<IPluginCatalog> _catalogs;

    public bool IsInitialized { get; private set; }

    public CompositePluginCatalog(params IPluginCatalog[] catalogs)
    {
        _catalogs = catalogs?.ToList() ?? [];
    }

    public CompositePluginCatalog(IEnumerable<IPluginCatalog> catalogs)
    {
        _catalogs = catalogs?.ToList() ?? [];
    }

    public void AddCatalog(IPluginCatalog catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        _catalogs.Add(catalog);
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        foreach (var catalog in _catalogs)
        {
            if (!catalog.IsInitialized)
            {
                await catalog.InitializeAsync();
            }
        }

        IsInitialized = true;
    }

    public IReadOnlyList<PluginInfo> GetPlugins()
    {
        return _catalogs.SelectMany(c => c.GetPlugins()).ToList();
    }

    public PluginInfo? Get(string name, Version? version = null)
    {
        foreach (var catalog in _catalogs)
        {
            var plugin = catalog.Get(name, version);
            if (plugin != null)
            {
                return plugin;
            }
        }
        return null;
    }
}
