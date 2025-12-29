using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Plugin catalog for a folder containing plugin assemblies
/// </summary>
public class FolderPluginCatalog : IPluginCatalog
{
    private readonly string _folderPath;
    private readonly FolderPluginCatalogOptions _options;
    private readonly ILogger? _logger;
    private readonly List<AssemblyPluginCatalog> _catalogs = [];

    public bool IsInitialized { get; private set; }

    public FolderPluginCatalog(string folderPath, FolderPluginCatalogOptions? options = null, ILogger? logger = null)
    {
        _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
        _options = options ?? new FolderPluginCatalogOptions();
        _logger = logger;
    }

    public FolderPluginCatalog(string folderPath, TypeFinderCriteria criteria, ILogger? logger = null)
        : this(folderPath, new FolderPluginCatalogOptions { TypeFinderCriteria = criteria }, logger)
    {
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        if (!Directory.Exists(_folderPath))
        {
            _logger?.LogWarning("Plugin folder does not exist: {FolderPath}", _folderPath);
            IsInitialized = true;
            return;
        }

        var foundFiles = new HashSet<string>();

        foreach (var pattern in _options.SearchPatterns)
        {
            var files = Directory.GetFiles(
                _folderPath,
                pattern,
                _options.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                foundFiles.Add(file);
            }
        }

        _logger?.LogDebug("Found {Count} potential plugin files in {FolderPath}", foundFiles.Count, _folderPath);

        foreach (var assemblyPath in foundFiles)
        {
            try
            {
                if (!IsPluginAssembly(assemblyPath))
                {
                    continue;
                }

                var catalog = new AssemblyPluginCatalog(
                    assemblyPath,
                    _options.TypeFinderCriteria,
                    _options.PluginLoadContextOptions,
                    _logger);

                await catalog.InitializeAsync();

                if (catalog.GetPlugins().Count > 0)
                {
                    _catalogs.Add(catalog);
                    _logger?.LogInformation("Loaded plugin assembly: {AssemblyPath} with {Count} plugins",
                        assemblyPath, catalog.GetPlugins().Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load plugin from {AssemblyPath}", assemblyPath);
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

    private bool IsPluginAssembly(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new PEReader(stream);

            if (!reader.HasMetadata)
            {
                return false;
            }

            // If no criteria specified, assume all DLLs with metadata are potential plugins
            if (_options.TypeFinderCriteria == null)
            {
                return true;
            }

            // For now, we'll load the assembly to check for types
            // A more efficient implementation would use MetadataLoadContext
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not read PE metadata from {AssemblyPath}", assemblyPath);
            return false;
        }
    }
}

/// <summary>
/// Options for the FolderPluginCatalog
/// </summary>
public class FolderPluginCatalogOptions
{
    /// <summary>
    /// Search patterns for finding plugin files
    /// </summary>
    public List<string> SearchPatterns { get; set; } = ["*.dll"];

    /// <summary>
    /// Whether to search in subfolders
    /// </summary>
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>
    /// Criteria for finding plugin types
    /// </summary>
    public TypeFinderCriteria? TypeFinderCriteria { get; set; }

    /// <summary>
    /// Options for the plugin load context
    /// </summary>
    public PluginLoadContextOptions PluginLoadContextOptions { get; set; } = new();
}
