using System.Reflection;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Represents a single Plugin. Each plugin has a name, version and .NET Type.
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Gets the name of the plugin
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the plugin version
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// Gets the .NET Type which is the plugin
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the plugin type's assembly
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Gets the catalog which contains the plugin
    /// </summary>
    public IPluginCatalog Source { get; }

    /// <summary>
    /// Gets the description of the plugin
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the tags of the plugin
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    public PluginInfo(
        Assembly assembly,
        Type type,
        string name,
        Version version,
        IPluginCatalog source,
        string? description = null,
        IEnumerable<string>? tags = null)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Description = description;
        Tags = tags?.ToList() ?? [];
    }

    public static implicit operator Type(PluginInfo plugin) => plugin.Type;

    public override string ToString() => $"{Name}: {Version}";
}
