using System.ComponentModel.DataAnnotations;

namespace LowlandTech.Foundry.Api.Data;

/// <summary>
/// Global settings for plugin hosting behavior.
/// </summary>
public class PluginHostingSettings
{
    [Key]
    public int Id { get; set; } = 1; // Singleton row

    /// <summary>
    /// Default hosting mode for plugins.
    /// Container = isolated Docker containers (enterprise)
    /// InProcess = loaded into API process (small backend, mobile)
    /// Desktop = loaded into app process with SQLite (Photino/MAUI)
    /// </summary>
    [MaxLength(32)]
    public string DefaultHostingMode { get; set; } = PluginHostingMode.Container;

    /// <summary>
    /// Base path for plugin API routes (e.g., "/api/plugins").
    /// </summary>
    [MaxLength(256)]
    public string PluginRouteBase { get; set; } = "/api/plugins";

    /// <summary>
    /// Docker network name for plugin containers.
    /// </summary>
    [MaxLength(128)]
    public string DockerNetwork { get; set; } = "foundry-plugins";

    /// <summary>
    /// Docker registry URL for plugin images.
    /// </summary>
    [MaxLength(512)]
    public string? DockerRegistry { get; set; }

    /// <summary>
    /// Whether to automatically apply database migrations when installing plugins.
    /// </summary>
    public bool AutoApplyMigrations { get; set; } = true;

    /// <summary>
    /// Whether to automatically activate plugins after installation.
    /// </summary>
    public bool AutoActivateOnInstall { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent plugin containers.
    /// </summary>
    public int MaxConcurrentContainers { get; set; } = 50;

    /// <summary>
    /// Default memory limit for plugin containers (in MB).
    /// </summary>
    public int DefaultContainerMemoryMb { get; set; } = 256;

    /// <summary>
    /// Default CPU limit for plugin containers (e.g., "0.5" for half a core).
    /// </summary>
    [MaxLength(16)]
    public string DefaultContainerCpuLimit { get; set; } = "0.5";

    /// <summary>
    /// Health check interval for plugin containers (in seconds).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for plugin container startup (in seconds).
    /// </summary>
    public int ContainerStartupTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// NuGet feed URLs for discovering plugins (semicolon-separated).
    /// </summary>
    [MaxLength(2048)]
    public string PluginFeedUrls { get; set; } = "";

    /// <summary>
    /// Whether multitenancy is enabled globally.
    /// </summary>
    public bool MultitenancyEnabled { get; set; } = true;

    /// <summary>
    /// Default tenant header name for multitenancy.
    /// </summary>
    [MaxLength(64)]
    public string TenantHeader { get; set; } = "X-Tenant-Id";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Plugin hosting modes.
/// </summary>
public static class PluginHostingMode
{
    /// <summary>
    /// Plugin runs in an isolated Docker container.
    /// Best for enterprise deployments with strict isolation requirements.
    /// </summary>
    public const string Container = "Container";

    /// <summary>
    /// Plugin loads into the API process directly.
    /// Best for small backends, mobile app backends, or development.
    /// </summary>
    public const string InProcess = "InProcess";

    /// <summary>
    /// Plugin loads into the desktop app process with local SQLite.
    /// Best for Photino or MAUI applications.
    /// </summary>
    public const string Desktop = "Desktop";
}
