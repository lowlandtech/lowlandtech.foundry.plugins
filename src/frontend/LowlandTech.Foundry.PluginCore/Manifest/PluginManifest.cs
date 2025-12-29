using System.Text.Json.Serialization;

namespace LowlandTech.Foundry.PluginCore.Manifest;

/// <summary>
/// Root plugin manifest extracted from OpenAPI document with x-plugin extensions.
/// This is the C# representation of the x-plugin vendor extension in openapi.yaml.
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Unique plugin identifier (e.g., "lowlandtech.crm").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version (semver format).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Plugin author/vendor.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Host API version compatibility (semver range, e.g., "^1.0.0").
    /// </summary>
    [JsonPropertyName("hostApiCompatibility")]
    public string? HostApiCompatibility { get; set; }

    /// <summary>
    /// Runtime configuration for the plugin.
    /// </summary>
    [JsonPropertyName("runtime")]
    public PluginRuntimeConfig Runtime { get; set; } = new();

    /// <summary>
    /// Persistence/database configuration.
    /// </summary>
    [JsonPropertyName("persistence")]
    public PluginPersistenceConfig Persistence { get; set; } = new();

    /// <summary>
    /// Multitenancy configuration.
    /// </summary>
    [JsonPropertyName("multitenancy")]
    public PluginMultitenancyConfig Multitenancy { get; set; } = new();

    /// <summary>
    /// Authentication/authorization configuration.
    /// </summary>
    [JsonPropertyName("auth")]
    public PluginAuthConfig Auth { get; set; } = new();

    /// <summary>
    /// Plugin icon URL or base64 data URI.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Screenshots for marketplace display.
    /// </summary>
    [JsonPropertyName("screenshots")]
    public List<string> Screenshots { get; set; } = [];

    /// <summary>
    /// Plugin homepage URL.
    /// </summary>
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    /// <summary>
    /// Plugin documentation URL.
    /// </summary>
    [JsonPropertyName("documentation")]
    public string? Documentation { get; set; }

    /// <summary>
    /// Plugin license identifier (SPDX).
    /// </summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }
}

/// <summary>
/// Runtime hosting configuration from x-plugin.runtime.
/// </summary>
public class PluginRuntimeConfig
{
    /// <summary>
    /// Hosting mode: "container" | "inprocess".
    /// </summary>
    [JsonPropertyName("hosting")]
    public string Hosting { get; set; } = "container";

    /// <summary>
    /// Base route for plugin API endpoints (e.g., "/api/plugins/lowlandtech.crm").
    /// </summary>
    [JsonPropertyName("routeBase")]
    public string RouteBase { get; set; } = string.Empty;

    /// <summary>
    /// Health check endpoint path relative to plugin base.
    /// </summary>
    [JsonPropertyName("healthPath")]
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// OpenAPI spec endpoint path relative to plugin base.
    /// </summary>
    [JsonPropertyName("openApiPath")]
    public string OpenApiPath { get; set; } = "/openapi.json";

    /// <summary>
    /// Container-specific configuration.
    /// </summary>
    [JsonPropertyName("container")]
    public ContainerConfig? Container { get; set; }
}

/// <summary>
/// Container-specific runtime configuration.
/// </summary>
public class ContainerConfig
{
    /// <summary>
    /// Base Docker image to use.
    /// </summary>
    [JsonPropertyName("baseImage")]
    public string BaseImage { get; set; } = "mcr.microsoft.com/dotnet/aspnet:10.0";

    /// <summary>
    /// Memory limit in MB.
    /// </summary>
    [JsonPropertyName("memoryMb")]
    public int MemoryMb { get; set; } = 256;

    /// <summary>
    /// CPU limit (e.g., "0.5" for half a core).
    /// </summary>
    [JsonPropertyName("cpuLimit")]
    public string CpuLimit { get; set; } = "0.5";

    /// <summary>
    /// Environment variables to set in the container.
    /// </summary>
    [JsonPropertyName("environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Volume mounts (host:container format).
    /// </summary>
    [JsonPropertyName("volumes")]
    public List<string> Volumes { get; set; } = [];
}

/// <summary>
/// Persistence configuration from x-plugin.persistence.
/// </summary>
public class PluginPersistenceConfig
{
    /// <summary>
    /// Persistence mode: "schema" | "database".
    /// Schema = isolated schema in host database (recommended).
    /// Database = separate database per plugin.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "schema";

    /// <summary>
    /// Schema name for schema-per-plugin mode (e.g., "plug_lowlandtech_crm").
    /// </summary>
    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; set; }

    /// <summary>
    /// Connection string reference: "HostDefault" or a named connection.
    /// </summary>
    [JsonPropertyName("connection")]
    public string Connection { get; set; } = "HostDefault";

    /// <summary>
    /// Migration configuration.
    /// </summary>
    [JsonPropertyName("migrations")]
    public MigrationConfig Migrations { get; set; } = new();
}

/// <summary>
/// Migration configuration.
/// </summary>
public class MigrationConfig
{
    /// <summary>
    /// Migration ownership: "plugin-owned" | "host-owned".
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "plugin-owned";

    /// <summary>
    /// Whether to automatically apply migrations on install.
    /// </summary>
    [JsonPropertyName("autoApplyOnInstall")]
    public bool AutoApplyOnInstall { get; set; } = true;
}

/// <summary>
/// Multitenancy configuration from x-plugin.multitenancy.
/// </summary>
public class PluginMultitenancyConfig
{
    /// <summary>
    /// Multitenancy mode: "none" | "row" | "schema" | "database".
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "row";

    /// <summary>
    /// Tenant key property name (e.g., "TenantId").
    /// </summary>
    [JsonPropertyName("tenantKey")]
    public string TenantKey { get; set; } = "TenantId";

    /// <summary>
    /// Enforcement configuration.
    /// </summary>
    [JsonPropertyName("enforcement")]
    public TenantEnforcementConfig Enforcement { get; set; } = new();
}

/// <summary>
/// Tenant enforcement configuration.
/// </summary>
public class TenantEnforcementConfig
{
    /// <summary>
    /// Entity names that require tenant filtering.
    /// </summary>
    [JsonPropertyName("applyToEntities")]
    public List<string> ApplyToEntities { get; set; } = [];

    /// <summary>
    /// Whether the tenant header is required on all requests.
    /// </summary>
    [JsonPropertyName("requireTenantHeader")]
    public bool RequireTenantHeader { get; set; } = true;

    /// <summary>
    /// HTTP header name for tenant ID.
    /// </summary>
    [JsonPropertyName("tenantHeader")]
    public string TenantHeader { get; set; } = "X-Tenant-Id";
}

/// <summary>
/// Authentication configuration from x-plugin.auth.
/// </summary>
public class PluginAuthConfig
{
    /// <summary>
    /// Default authentication scheme (e.g., "Bearer").
    /// </summary>
    [JsonPropertyName("defaultScheme")]
    public string DefaultScheme { get; set; } = "Bearer";

    /// <summary>
    /// Default scopes required for all endpoints.
    /// </summary>
    [JsonPropertyName("defaultScopes")]
    public List<string> DefaultScopes { get; set; } = [];
}
