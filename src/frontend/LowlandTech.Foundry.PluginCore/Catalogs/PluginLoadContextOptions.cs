using System.Reflection;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Options for controlling how plugins are loaded into the application
/// </summary>
public class PluginLoadContextOptions
{
    /// <summary>
    /// Default options that apply to all plugin load contexts
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// Additional runtime paths to search for assemblies
        /// </summary>
        public static List<string>? AdditionalRuntimePaths { get; set; }
    }

    /// <summary>
    /// How to handle assemblies from the host application
    /// </summary>
    public UseHostApplicationAssemblies UseHostApplicationAssemblies { get; set; } = UseHostApplicationAssemblies.PreferPlugin;

    /// <summary>
    /// Specific assemblies to load from the host application when UseHostApplicationAssemblies is Selected
    /// </summary>
    public List<AssemblyName>? HostApplicationAssemblies { get; set; }

    /// <summary>
    /// Additional paths to search for runtime assemblies
    /// </summary>
    public List<string>? AdditionalRuntimePaths { get; set; }

    /// <summary>
    /// Factory for creating loggers
    /// </summary>
    public Func<ILogger>? LoggerFactory { get; set; }
}

/// <summary>
/// Determines how host application assemblies are used when loading plugins
/// </summary>
public enum UseHostApplicationAssemblies
{
    /// <summary>
    /// Never use host application assemblies, always load from plugin
    /// </summary>
    Never,

    /// <summary>
    /// Always prefer host application assemblies over plugin assemblies
    /// </summary>
    Always,

    /// <summary>
    /// Only use selected host application assemblies
    /// </summary>
    Selected,

    /// <summary>
    /// Prefer plugin assemblies but fall back to host if not found
    /// </summary>
    PreferPlugin
}
