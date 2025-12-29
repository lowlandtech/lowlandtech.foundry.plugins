using LowlandTech.Foundry.PluginCore.Manifest;

namespace LowlandTech.Foundry.PluginCore.Hosting;

/// <summary>
/// Service for installing, updating, and removing plugin packages.
/// Coordinates between the NuGet feed, manifest parsing, and plugin hosts.
/// </summary>
public interface IPluginPackageInstaller
{
    /// <summary>
    /// Installs a plugin from a NuGet package.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="version">The version to install (null for latest).</param>
    /// <param name="feedUrl">Optional feed URL (uses configured feeds if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PackageInstallResult> InstallFromFeedAsync(
        string packageId,
        string? version = null,
        string? feedUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a plugin from a local .nupkg file.
    /// </summary>
    /// <param name="packagePath">Path to the .nupkg file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PackageInstallResult> InstallFromFileAsync(
        string packagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a plugin to a new version.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="targetVersion">The target version (null for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PackageUpdateResult> UpdateAsync(
        string pluginId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a plugin and optionally removes its data.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="removeData">Whether to remove the database schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PackageUninstallResult> UninstallAsync(
        string pluginId,
        bool removeData = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available plugins from configured feeds.
    /// </summary>
    /// <param name="searchQuery">Optional search query.</param>
    /// <param name="skip">Number of results to skip.</param>
    /// <param name="take">Number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginSearchResult> SearchAvailableAsync(
        string? searchQuery = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details about an available plugin package.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The version (null for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginPackageInfo?> GetPackageInfoAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all installed plugins.
    /// </summary>
    Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledPluginsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for available updates for installed plugins.
    /// </summary>
    Task<IReadOnlyList<PluginUpdateInfo>> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a package installation.
/// </summary>
public record PackageInstallResult
{
    public bool Success { get; init; }
    public string? PluginId { get; init; }
    public string? Version { get; init; }
    public string? ErrorMessage { get; init; }
    public PluginManifest? Manifest { get; init; }
    public PluginInstallResult? InstallResult { get; init; }
    public PluginActivationResult? ActivationResult { get; init; }
}

/// <summary>
/// Result of a package update.
/// </summary>
public record PackageUpdateResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> AppliedMigrations { get; init; } = [];
}

/// <summary>
/// Result of a package uninstallation.
/// </summary>
public record PackageUninstallResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public bool DataRemoved { get; init; }
}

/// <summary>
/// Search results from plugin feeds.
/// </summary>
public record PluginSearchResult
{
    public List<PluginPackageInfo> Packages { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Information about an available plugin package.
/// </summary>
public record PluginPackageInfo
{
    public string PackageId { get; init; } = string.Empty;
    public string PluginId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? IconUrl { get; init; }
    public List<string> Tags { get; init; } = [];
    public DateTime? PublishedAt { get; init; }
    public long DownloadCount { get; init; }
    public string? LicenseUrl { get; init; }
    public string? ProjectUrl { get; init; }
    public List<string> AvailableVersions { get; init; } = [];
    public string? FeedUrl { get; init; }
}

/// <summary>
/// Information about an installed plugin.
/// </summary>
public record InstalledPluginInfo
{
    public string PluginId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string HostingMode { get; init; } = string.Empty;
    public string RuntimeStatus { get; init; } = string.Empty;
    public string? BaseUrl { get; init; }
    public DateTime InstalledAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public bool HasUpdate { get; init; }
    public string? LatestVersion { get; init; }
}

/// <summary>
/// Information about an available update.
/// </summary>
public record PluginUpdateInfo
{
    public string PluginId { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? ReleaseNotes { get; init; }
    public DateTime? PublishedAt { get; init; }
}
