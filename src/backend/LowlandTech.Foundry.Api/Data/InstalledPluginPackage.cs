using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LowlandTech.Foundry.Api.Data;

/// <summary>
/// Represents an installed plugin package with its manifest and hosting configuration.
/// </summary>
public class InstalledPluginPackage
{
    [Key]
    [MaxLength(256)]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the plugin.
    /// </summary>
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version (semver format).
    /// </summary>
    [MaxLength(64)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Plugin description.
    /// </summary>
    [MaxLength(2048)]
    public string? Description { get; set; }

    /// <summary>
    /// Plugin author/vendor.
    /// </summary>
    [MaxLength(256)]
    public string? Author { get; set; }

    /// <summary>
    /// NuGet package ID if installed from a feed.
    /// </summary>
    [MaxLength(256)]
    public string? PackageId { get; set; }

    /// <summary>
    /// Source feed URL where the package was downloaded from.
    /// </summary>
    [MaxLength(512)]
    public string? SourceFeedUrl { get; set; }

    /// <summary>
    /// Hosting mode for this specific plugin (overrides global default).
    /// </summary>
    [MaxLength(32)]
    public string HostingMode { get; set; } = PluginHostingMode.Container;

    /// <summary>
    /// Base route for this plugin's API endpoints.
    /// </summary>
    [MaxLength(256)]
    public string RouteBase { get; set; } = string.Empty;

    /// <summary>
    /// Database schema name for this plugin's entities.
    /// </summary>
    [MaxLength(128)]
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>
    /// The full OpenAPI manifest with x-plugin extensions (JSON).
    /// </summary>
    public string? OpenApiManifest { get; set; }

    /// <summary>
    /// Docker image name for container-hosted plugins.
    /// </summary>
    [MaxLength(512)]
    public string? DockerImage { get; set; }

    /// <summary>
    /// Docker container ID when running.
    /// </summary>
    [MaxLength(128)]
    public string? ContainerId { get; set; }

    /// <summary>
    /// Container port mapping (host:container).
    /// </summary>
    [MaxLength(64)]
    public string? ContainerPort { get; set; }

    /// <summary>
    /// Path to the plugin assembly for in-process hosting.
    /// </summary>
    [MaxLength(1024)]
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Entry point class name (fully qualified) for the IPlugin implementation.
    /// </summary>
    [MaxLength(512)]
    public string? EntryPointClass { get; set; }

    /// <summary>
    /// Health endpoint path relative to plugin base.
    /// </summary>
    [MaxLength(256)]
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// OpenAPI endpoint path relative to plugin base.
    /// </summary>
    [MaxLength(256)]
    public string OpenApiPath { get; set; } = "/openapi.json";

    /// <summary>
    /// Host API version compatibility (semver range).
    /// </summary>
    [MaxLength(64)]
    public string? HostApiCompatibility { get; set; }

    /// <summary>
    /// Whether migrations have been applied.
    /// </summary>
    public bool MigrationsApplied { get; set; }

    /// <summary>
    /// Last migration that was applied.
    /// </summary>
    [MaxLength(256)]
    public string? LastAppliedMigration { get; set; }

    /// <summary>
    /// Current runtime status of the plugin.
    /// </summary>
    [MaxLength(32)]
    public string RuntimeStatus { get; set; } = PluginRuntimeStatus.Stopped;

    /// <summary>
    /// Last health check result.
    /// </summary>
    [MaxLength(32)]
    public string? LastHealthStatus { get; set; }

    /// <summary>
    /// Last health check timestamp.
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Error message if the plugin is in an error state.
    /// </summary>
    [MaxLength(4096)]
    public string? ErrorMessage { get; set; }

    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Navigation to the plugin state.
    /// </summary>
    [ForeignKey(nameof(PluginId))]
    public PluginStateEntity? PluginState { get; set; }
}

/// <summary>
/// Runtime status of a plugin.
/// </summary>
public static class PluginRuntimeStatus
{
    public const string Stopped = "Stopped";
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string Stopping = "Stopping";
    public const string Error = "Error";
    public const string Unhealthy = "Unhealthy";
}
