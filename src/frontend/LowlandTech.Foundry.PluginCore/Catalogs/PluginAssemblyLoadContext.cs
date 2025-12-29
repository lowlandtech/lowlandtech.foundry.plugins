using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Defines a Plugin Load Context which allows the loading of plugin specific versions of assemblies.
/// </summary>
public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginPath;
    private readonly AssemblyDependencyResolver _resolver;
    private readonly PluginLoadContextOptions _options;
    private ILogger? _logger;
    private static readonly object LoggerLock = new();

    public PluginAssemblyLoadContext(string pluginPath, PluginLoadContextOptions? options = null)
        : base(isCollectible: true)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _options = options ?? new PluginLoadContextOptions();
    }

    public PluginAssemblyLoadContext(Assembly assembly, PluginLoadContextOptions? options = null)
        : this(assembly.Location, options)
    {
    }

    public Assembly Load()
    {
        var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(_pluginPath));
        return LoadFromAssemblyName(assemblyName);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        Log(LogLevel.Debug, "Loading {AssemblyName}", assemblyName);

        if (TryUseHostApplicationAssembly(assemblyName))
        {
            if (LoadHostApplicationAssembly(assemblyName))
            {
                Log(LogLevel.Debug, "Assembly {AssemblyName} available through host application's AssemblyLoadContext", assemblyName);
                return null;
            }

            Log(LogLevel.Debug, "Host application's AssemblyLoadContext doesn't contain {AssemblyName}. Trying plugin's references.", assemblyName);
        }

        // Try to resolve from the plugin's dependencies
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath != null)
        {
            Log(LogLevel.Debug, "Loading {AssemblyName} from {Path}", assemblyName, assemblyPath);
            return LoadFromAssemblyPath(assemblyPath);
        }

        // If PreferPlugin, try host application as fallback
        if (_options.UseHostApplicationAssemblies == UseHostApplicationAssemblies.PreferPlugin)
        {
            if (LoadHostApplicationAssembly(assemblyName))
            {
                Log(LogLevel.Debug, "Assembly {AssemblyName} not in plugin references but found in host application", assemblyName);
                return null;
            }
        }

        // Try additional runtime paths
        if (_options.AdditionalRuntimePaths?.Count > 0)
        {
            var fileName = $"{assemblyName.Name}.dll";
            foreach (var runtimePath in _options.AdditionalRuntimePaths)
            {
                var filePath = Directory.GetFiles(runtimePath, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (filePath != null)
                {
                    Log(LogLevel.Debug, "Found {AssemblyName} at {AssemblyPath} using additional runtime path {RuntimePath}",
                        assemblyName, filePath, runtimePath);
                    return LoadFromAssemblyPath(filePath);
                }
            }
        }

        // Try default additional paths
        if (PluginLoadContextOptions.Defaults.AdditionalRuntimePaths?.Count > 0)
        {
            var fileName = $"{assemblyName.Name}.dll";
            foreach (var runtimePath in PluginLoadContextOptions.Defaults.AdditionalRuntimePaths)
            {
                var filePath = Directory.GetFiles(runtimePath, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (filePath != null)
                {
                    Log(LogLevel.Debug, "Found {AssemblyName} at {AssemblyPath} using default additional runtime path",
                        assemblyName, filePath);
                    return LoadFromAssemblyPath(filePath);
                }
            }
        }

        Log(LogLevel.Warning, "Couldn't locate assembly {AssemblyName}", assemblyName);
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private bool TryUseHostApplicationAssembly(AssemblyName assemblyName)
    {
        return _options.UseHostApplicationAssemblies switch
        {
            UseHostApplicationAssemblies.Never => false,
            UseHostApplicationAssemblies.Always => true,
            UseHostApplicationAssemblies.Selected =>
                _options.HostApplicationAssemblies?.Any(x =>
                    string.Equals(x.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)) == true,
            UseHostApplicationAssemblies.PreferPlugin => false,
            _ => false
        };
    }

    private static bool LoadHostApplicationAssembly(AssemblyName assemblyName)
    {
        try
        {
            Default.LoadFromAssemblyName(assemblyName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Log(LogLevel logLevel, string message, params object?[] args)
    {
        var logger = GetLogger();
        logger.Log(logLevel, message, args);
    }

    private ILogger GetLogger()
    {
        if (_logger != null) return _logger;

        lock (LoggerLock)
        {
            _logger ??= _options.LoggerFactory?.Invoke() ?? NullLogger.Instance;
        }

        return _logger;
    }
}
