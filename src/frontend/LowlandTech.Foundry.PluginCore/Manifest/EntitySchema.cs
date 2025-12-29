using System.Text.Json.Serialization;

namespace LowlandTech.Foundry.PluginCore.Manifest;

/// <summary>
/// Persistence configuration from x-persistence on OpenAPI schema objects.
/// Defines how a schema maps to database entities.
/// </summary>
public class EntityPersistenceConfig
{
    /// <summary>
    /// Entity kind: "entity" | "owned" | "projection" | "request".
    /// - entity: Persisted table with primary key
    /// - owned: Embedded in parent entity (no separate table)
    /// - projection: Read-only view or computed
    /// - request: DTO, not persisted
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "entity";

    /// <summary>
    /// Database table name.
    /// </summary>
    [JsonPropertyName("table")]
    public string? Table { get; set; }

    /// <summary>
    /// Primary key configuration.
    /// </summary>
    [JsonPropertyName("key")]
    public KeyConfig? Key { get; set; }

    /// <summary>
    /// Concurrency control configuration.
    /// </summary>
    [JsonPropertyName("concurrency")]
    public ConcurrencyConfig? Concurrency { get; set; }

    /// <summary>
    /// Soft delete configuration.
    /// </summary>
    [JsonPropertyName("softDelete")]
    public SoftDeleteConfig? SoftDelete { get; set; }

    /// <summary>
    /// Index definitions.
    /// </summary>
    [JsonPropertyName("indexes")]
    public List<IndexConfig> Indexes { get; set; } = [];

    /// <summary>
    /// Relationship definitions.
    /// </summary>
    [JsonPropertyName("relationships")]
    public List<RelationshipConfig> Relationships { get; set; } = [];
}

/// <summary>
/// Primary key configuration.
/// </summary>
public class KeyConfig
{
    /// <summary>
    /// Key generation strategy: "guid" | "int" | "nodekey" | "composite".
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "guid";

    /// <summary>
    /// Property name for the primary key.
    /// </summary>
    [JsonPropertyName("property")]
    public string Property { get; set; } = "Id";

    /// <summary>
    /// Key generation: "server" | "client".
    /// </summary>
    [JsonPropertyName("generated")]
    public string Generated { get; set; } = "server";

    /// <summary>
    /// For composite keys, the list of property names.
    /// </summary>
    [JsonPropertyName("properties")]
    public List<string>? Properties { get; set; }
}

/// <summary>
/// Concurrency control configuration.
/// </summary>
public class ConcurrencyConfig
{
    /// <summary>
    /// Concurrency mode: "none" | "rowversion".
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "none";

    /// <summary>
    /// Property name for the concurrency token.
    /// </summary>
    [JsonPropertyName("property")]
    public string? Property { get; set; }
}

/// <summary>
/// Soft delete configuration.
/// </summary>
public class SoftDeleteConfig
{
    /// <summary>
    /// Whether soft delete is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Property name for the soft delete flag.
    /// </summary>
    [JsonPropertyName("property")]
    public string Property { get; set; } = "IsDeleted";
}

/// <summary>
/// Index configuration.
/// </summary>
public class IndexConfig
{
    /// <summary>
    /// Index name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the index is unique.
    /// </summary>
    [JsonPropertyName("unique")]
    public bool Unique { get; set; }

    /// <summary>
    /// Properties included in the index.
    /// </summary>
    [JsonPropertyName("properties")]
    public List<string> Properties { get; set; } = [];

    /// <summary>
    /// SQL filter expression for partial indexes.
    /// </summary>
    [JsonPropertyName("filter")]
    public string? Filter { get; set; }
}

/// <summary>
/// Relationship configuration.
/// </summary>
public class RelationshipConfig
{
    /// <summary>
    /// Navigation property name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Relationship type: "one-to-one" | "one-to-many" | "many-to-one" | "many-to-many".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "many-to-one";

    /// <summary>
    /// Target entity name.
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key property name.
    /// </summary>
    [JsonPropertyName("foreignKey")]
    public string? ForeignKey { get; set; }

    /// <summary>
    /// Whether the relationship is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>
    /// Delete behavior: "cascade" | "restrict" | "setNull" | "noAction".
    /// </summary>
    [JsonPropertyName("cascade")]
    public string? Cascade { get; set; }

    /// <summary>
    /// Delete behavior (alias for cascade).
    /// </summary>
    [JsonPropertyName("onDelete")]
    public string? OnDelete { get; set; }
}

/// <summary>
/// Tenant property configuration from x-tenant.
/// </summary>
public class TenantPropertyConfig
{
    /// <summary>
    /// Whether the tenant ID is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    /// <summary>
    /// Source of the tenant ID: "header" | "claim" | "route".
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "header";
}

/// <summary>
/// Validation configuration from x-validation.
/// </summary>
public class ValidationConfig
{
    /// <summary>
    /// Validation rules.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<ValidationRule> Rules { get; set; } = [];
}

/// <summary>
/// Individual validation rule.
/// </summary>
public class ValidationRule
{
    /// <summary>
    /// Rule type: "notEmpty" | "email" | "uuid" | "regex" | "range" | "length" | "atLeastOneOf".
    /// </summary>
    [JsonPropertyName("rule")]
    public string Rule { get; set; } = string.Empty;

    /// <summary>
    /// Field to apply the rule to.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>
    /// Fields for multi-field rules like "atLeastOneOf".
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string>? Fields { get; set; }

    /// <summary>
    /// Validation error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Regex pattern for "regex" rule.
    /// </summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>
    /// Minimum value/length for "range" or "length" rule.
    /// </summary>
    [JsonPropertyName("min")]
    public int? Min { get; set; }

    /// <summary>
    /// Maximum value/length for "range" or "length" rule.
    /// </summary>
    [JsonPropertyName("max")]
    public int? Max { get; set; }
}

/// <summary>
/// Endpoint auth configuration from x-auth.
/// </summary>
public class EndpointAuthConfig
{
    /// <summary>
    /// Required scopes for this endpoint.
    /// </summary>
    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Required roles for this endpoint.
    /// </summary>
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

    /// <summary>
    /// Whether the endpoint allows anonymous access.
    /// </summary>
    [JsonPropertyName("allowAnonymous")]
    public bool AllowAnonymous { get; set; }
}
