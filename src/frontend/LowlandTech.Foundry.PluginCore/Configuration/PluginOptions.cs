namespace LowlandTech.Foundry.PluginCore.Configuration;

/// <summary>
/// Configuration options for the plugin system
/// </summary>
public class PluginOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Plugins";

    /// <summary>
    /// Folder paths to scan for plugin assemblies
    /// </summary>
    public List<PluginFolderConfig> Folders { get; set; } = [];

    /// <summary>
    /// NuGet packages to load as plugins
    /// </summary>
    public List<PluginNuGetConfig> NuGetPackages { get; set; } = [];

    /// <summary>
    /// Additional NuGet feeds to search for packages
    /// </summary>
    public List<string> NuGetFeeds { get; set; } = [];

    /// <summary>
    /// Path to store downloaded NuGet packages
    /// </summary>
    public string? PackagesFolder { get; set; }

    /// <summary>
    /// Whether to include prerelease NuGet packages
    /// </summary>
    public bool IncludePrerelease { get; set; }

    /// <summary>
    /// Target framework for NuGet package resolution
    /// </summary>
    public string? TargetFramework { get; set; }
}

/// <summary>
/// Configuration for a plugin folder
/// </summary>
public class PluginFolderConfig
{
    /// <summary>
    /// Path to the folder containing plugin assemblies
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Whether to search subfolders
    /// </summary>
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>
    /// File patterns to search for (e.g., "*.Plugin.dll")
    /// </summary>
    public List<string> SearchPatterns { get; set; } = ["*.dll"];
}

/// <summary>
/// Configuration for a NuGet plugin package
/// </summary>
public class PluginNuGetConfig
{
    /// <summary>
    /// NuGet package ID
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Specific version to use (null for latest)
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Whether to include prerelease versions for this package
    /// </summary>
    public bool? IncludePrerelease { get; set; }
}
