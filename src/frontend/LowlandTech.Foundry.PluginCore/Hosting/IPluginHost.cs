using LowlandTech.Foundry.PluginCore.Manifest;

namespace LowlandTech.Foundry.PluginCore.Hosting;

/// <summary>
/// Abstracts plugin hosting across different deployment modes.
/// Implementations handle container orchestration, in-process loading, or desktop hosting.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// Gets the hosting mode this host supports.
    /// </summary>
    string HostingMode { get; }

    /// <summary>
    /// Installs a plugin from its manifest.
    /// Creates the database schema, applies migrations, and prepares for activation.
    /// </summary>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="packagePath">Path to the extracted plugin package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installation result.</returns>
    Task<PluginInstallResult> InstallAsync(
        PluginManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a plugin.
    /// Stops the plugin, removes the database schema, and cleans up resources.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="removeData">Whether to remove the database schema and data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        bool removeData = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a plugin.
    /// For containers: starts the container.
    /// For in-process: loads the assembly and initializes services.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginActivationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a plugin.
    /// For containers: stops the container.
    /// For in-process: unloads the assembly.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginDeactivationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginHealthResult> CheckHealthAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the runtime status of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginRuntimeInfo> GetRuntimeInfoAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the base URL for a plugin's API endpoints.
    /// For containers: the reverse proxy URL.
    /// For in-process: the local route base.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    string GetPluginBaseUrl(string pluginId);
}

/// <summary>
/// Result of a plugin installation.
/// </summary>
public record PluginInstallResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? SchemaName { get; init; }
    public List<string> AppliedMigrations { get; init; } = [];
}

/// <summary>
/// Result of a plugin uninstallation.
/// </summary>
public record PluginUninstallResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public bool DataRemoved { get; init; }
}

/// <summary>
/// Result of plugin activation.
/// </summary>
public record PluginActivationResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? ContainerId { get; init; }
    public string? BaseUrl { get; init; }
    public TimeSpan StartupDuration { get; init; }
}

/// <summary>
/// Result of plugin deactivation.
/// </summary>
public record PluginDeactivationResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Health check result for a plugin.
/// </summary>
public record PluginHealthResult
{
    public bool IsHealthy { get; init; }
    public string PluginId { get; init; } = string.Empty;
    public string Status { get; init; } = "Unknown";
    public string? Message { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Runtime information about a plugin.
/// </summary>
public record PluginRuntimeInfo
{
    public string PluginId { get; init; } = string.Empty;
    public string Status { get; init; } = "Unknown";
    public string HostingMode { get; init; } = string.Empty;
    public string? ContainerId { get; init; }
    public string? BaseUrl { get; init; }
    public DateTime? StartedAt { get; init; }
    public TimeSpan? Uptime { get; init; }
    public long? MemoryUsageBytes { get; init; }
    public double? CpuUsagePercent { get; init; }
    public int? RequestCount { get; init; }
    public int? ErrorCount { get; init; }
}

/// <summary>
/// Factory for creating the appropriate plugin host based on hosting mode.
/// </summary>
public interface IPluginHostFactory
{
    /// <summary>
    /// Gets a plugin host for the specified hosting mode.
    /// </summary>
    /// <param name="hostingMode">The hosting mode.</param>
    /// <returns>The plugin host.</returns>
    IPluginHost GetHost(string hostingMode);

    /// <summary>
    /// Gets all registered plugin hosts.
    /// </summary>
    IEnumerable<IPluginHost> GetAllHosts();
}
