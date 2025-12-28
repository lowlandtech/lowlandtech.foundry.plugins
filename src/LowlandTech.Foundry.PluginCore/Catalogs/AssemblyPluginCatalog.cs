using System.Reflection;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Plugin catalog for a single assembly
/// </summary>
public class AssemblyPluginCatalog : IPluginCatalog
{
    private readonly string? _assemblyPath;
    private Assembly? _assembly;
    private readonly TypeFinderCriteria? _criteria;
    private readonly PluginLoadContextOptions _loadContextOptions;
    private readonly ILogger? _logger;
    private PluginAssemblyLoadContext? _loadContext;
    private List<PluginInfo> _plugins = [];

    public bool IsInitialized { get; private set; }

    public AssemblyPluginCatalog(string assemblyPath, TypeFinderCriteria? criteria = null,
        PluginLoadContextOptions? loadContextOptions = null, ILogger? logger = null)
    {
        _assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        _criteria = criteria;
        _loadContextOptions = loadContextOptions ?? new PluginLoadContextOptions();
        _logger = logger;
    }

    public AssemblyPluginCatalog(Assembly assembly, TypeFinderCriteria? criteria = null,
        PluginLoadContextOptions? loadContextOptions = null, ILogger? logger = null)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _assemblyPath = assembly.Location;
        _criteria = criteria;
        _loadContextOptions = loadContextOptions ?? new PluginLoadContextOptions();
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        _plugins = [];

        // Load assembly if needed
        if (_assembly == null && !string.IsNullOrWhiteSpace(_assemblyPath))
        {
            if (!File.Exists(_assemblyPath))
            {
                throw new FileNotFoundException($"Assembly not found: {_assemblyPath}");
            }

            _loadContext = new PluginAssemblyLoadContext(_assemblyPath, _loadContextOptions);
            _assembly = _loadContext.Load();
        }

        if (_assembly == null)
        {
            throw new InvalidOperationException("Failed to load assembly");
        }

        // Find plugin types
        var types = FindPluginTypes(_assembly);

        foreach (var type in types)
        {
            var assemblyName = _assembly.GetName();
            var plugin = new PluginInfo(
                assembly: _assembly,
                type: type,
                name: type.FullName ?? type.Name,
                version: assemblyName.Version ?? new Version(1, 0, 0),
                source: this,
                description: GetDescription(type),
                tags: _criteria?.Tags
            );

            _plugins.Add(plugin);
            _logger?.LogDebug("Discovered plugin: {PluginName} v{Version}", plugin.Name, plugin.Version);
        }

        IsInitialized = true;
        await Task.CompletedTask;
    }

    public IReadOnlyList<PluginInfo> GetPlugins() => _plugins;

    public PluginInfo? Get(string name, Version? version = null)
    {
        return _plugins.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (version == null || p.Version == version));
    }

    private IEnumerable<Type> FindPluginTypes(Assembly assembly)
    {
        try
        {
            var types = assembly.GetExportedTypes()
                .Where(t => t.IsClass && !t.IsAbstract);

            if (_criteria != null)
            {
                types = types.Where(t => _criteria.IsMatch(t));
            }

            return types;
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger?.LogWarning(ex, "Failed to load some types from assembly {Assembly}", assembly.FullName);
            return ex.Types.Where(t => t != null).Cast<Type>();
        }
    }

    private static string? GetDescription(Type type)
    {
        var descAttr = type.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "DescriptionAttribute");

        return descAttr?.GetType().GetProperty("Description")?.GetValue(descAttr) as string;
    }
}
