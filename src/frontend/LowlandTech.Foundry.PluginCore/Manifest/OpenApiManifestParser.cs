using System.Text.Json;
using System.Text.Json.Nodes;

namespace LowlandTech.Foundry.PluginCore.Manifest;

/// <summary>
/// Parses OpenAPI documents with x-plugin vendor extensions.
/// </summary>
public class OpenApiManifestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Parses a plugin manifest from an OpenAPI JSON document.
    /// </summary>
    /// <param name="openApiJson">The OpenAPI document as JSON string.</param>
    /// <returns>The parsed plugin manifest.</returns>
    public PluginManifest ParseFromJson(string openApiJson)
    {
        var doc = JsonNode.Parse(openApiJson)
            ?? throw new InvalidOperationException("Failed to parse OpenAPI JSON");

        return ParseFromJsonNode(doc);
    }

    /// <summary>
    /// Parses a plugin manifest from an OpenAPI JsonNode.
    /// </summary>
    public PluginManifest ParseFromJsonNode(JsonNode doc)
    {
        var manifest = new PluginManifest();

        // Extract from info section
        var info = doc["info"];
        if (info != null)
        {
            manifest.DisplayName = info["title"]?.GetValue<string>() ?? "";
            manifest.Version = info["version"]?.GetValue<string>() ?? "1.0.0";
            manifest.Description = info["description"]?.GetValue<string>();
        }

        // Extract x-plugin extension
        var xPlugin = doc["x-plugin"];
        if (xPlugin != null)
        {
            manifest.Id = xPlugin["id"]?.GetValue<string>() ?? "";

            if (xPlugin["displayName"] != null)
                manifest.DisplayName = xPlugin["displayName"]!.GetValue<string>();

            if (xPlugin["version"] != null)
                manifest.Version = xPlugin["version"]!.GetValue<string>();

            manifest.HostApiCompatibility = xPlugin["hostApiCompatibility"]?.GetValue<string>();
            manifest.Author = xPlugin["author"]?.GetValue<string>();
            manifest.Icon = xPlugin["icon"]?.GetValue<string>();
            manifest.Homepage = xPlugin["homepage"]?.GetValue<string>();
            manifest.Documentation = xPlugin["documentation"]?.GetValue<string>();
            manifest.License = xPlugin["license"]?.GetValue<string>();

            // Parse tags
            if (xPlugin["tags"] is JsonArray tagsArray)
            {
                manifest.Tags = tagsArray.Select(t => t?.GetValue<string>() ?? "").ToList();
            }

            // Parse screenshots
            if (xPlugin["screenshots"] is JsonArray screenshotsArray)
            {
                manifest.Screenshots = screenshotsArray.Select(s => s?.GetValue<string>() ?? "").ToList();
            }

            // Parse runtime config
            if (xPlugin["runtime"] != null)
            {
                manifest.Runtime = ParseRuntime(xPlugin["runtime"]!);
            }

            // Parse persistence config
            if (xPlugin["persistence"] != null)
            {
                manifest.Persistence = ParsePersistence(xPlugin["persistence"]!);
            }

            // Parse multitenancy config
            if (xPlugin["multitenancy"] != null)
            {
                manifest.Multitenancy = ParseMultitenancy(xPlugin["multitenancy"]!);
            }

            // Parse auth config
            if (xPlugin["auth"] != null)
            {
                manifest.Auth = ParseAuth(xPlugin["auth"]!);
            }
        }

        return manifest;
    }

    /// <summary>
    /// Extracts all entity schemas from the OpenAPI components/schemas section.
    /// </summary>
    public Dictionary<string, EntityDefinition> ParseEntitySchemas(string openApiJson)
    {
        var doc = JsonNode.Parse(openApiJson)
            ?? throw new InvalidOperationException("Failed to parse OpenAPI JSON");

        return ParseEntitySchemas(doc);
    }

    /// <summary>
    /// Extracts all entity schemas from an OpenAPI JsonNode.
    /// </summary>
    public Dictionary<string, EntityDefinition> ParseEntitySchemas(JsonNode doc)
    {
        var entities = new Dictionary<string, EntityDefinition>();

        var schemas = doc["components"]?["schemas"];
        if (schemas == null) return entities;

        foreach (var (name, schema) in schemas.AsObject())
        {
            if (schema == null) continue;

            var entity = new EntityDefinition
            {
                Name = name,
                Type = schema["type"]?.GetValue<string>() ?? "object"
            };

            // Parse x-persistence
            if (schema["x-persistence"] != null)
            {
                entity.Persistence = ParseEntityPersistence(schema["x-persistence"]!);
            }

            // Parse x-validation
            if (schema["x-validation"] != null)
            {
                entity.Validation = ParseValidation(schema["x-validation"]!);
            }

            // Parse required fields
            if (schema["required"] is JsonArray requiredArray)
            {
                entity.RequiredProperties = requiredArray.Select(r => r?.GetValue<string>() ?? "").ToList();
            }

            // Parse properties
            if (schema["properties"] != null)
            {
                entity.Properties = ParseProperties(schema["properties"]!);
            }

            entities[name] = entity;
        }

        return entities;
    }

    /// <summary>
    /// Extracts all endpoint definitions from OpenAPI paths.
    /// </summary>
    public List<EndpointDefinition> ParseEndpoints(string openApiJson)
    {
        var doc = JsonNode.Parse(openApiJson)
            ?? throw new InvalidOperationException("Failed to parse OpenAPI JSON");

        return ParseEndpoints(doc);
    }

    /// <summary>
    /// Extracts all endpoint definitions from an OpenAPI JsonNode.
    /// </summary>
    public List<EndpointDefinition> ParseEndpoints(JsonNode doc)
    {
        var endpoints = new List<EndpointDefinition>();

        var paths = doc["paths"];
        if (paths == null) return endpoints;

        foreach (var (path, pathItem) in paths.AsObject())
        {
            if (pathItem == null) continue;

            foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
            {
                var operation = pathItem[method];
                if (operation == null) continue;

                var endpoint = new EndpointDefinition
                {
                    Path = path,
                    Method = method.ToUpperInvariant(),
                    OperationId = operation["operationId"]?.GetValue<string>(),
                    Summary = operation["summary"]?.GetValue<string>(),
                    Description = operation["description"]?.GetValue<string>()
                };

                // Parse tags
                if (operation["tags"] is JsonArray tagsArray)
                {
                    endpoint.Tags = tagsArray.Select(t => t?.GetValue<string>() ?? "").ToList();
                }

                // Parse x-auth
                if (operation["x-auth"] != null)
                {
                    endpoint.Auth = ParseEndpointAuth(operation["x-auth"]!);
                }

                // Parse parameters
                if (operation["parameters"] is JsonArray paramsArray)
                {
                    endpoint.Parameters = paramsArray
                        .Where(p => p != null)
                        .Select(p => ParseParameter(p!))
                        .ToList();
                }

                // Parse request body
                if (operation["requestBody"] != null)
                {
                    endpoint.RequestBody = ParseRequestBody(operation["requestBody"]!);
                }

                // Parse responses
                if (operation["responses"] != null)
                {
                    endpoint.Responses = ParseResponses(operation["responses"]!);
                }

                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    private PluginRuntimeConfig ParseRuntime(JsonNode node)
    {
        var config = new PluginRuntimeConfig
        {
            Hosting = node["hosting"]?.GetValue<string>() ?? "container",
            RouteBase = node["routeBase"]?.GetValue<string>() ?? "",
            HealthPath = node["healthPath"]?.GetValue<string>() ?? "/health",
            OpenApiPath = node["openApiPath"]?.GetValue<string>() ?? "/openapi.json"
        };

        if (node["container"] != null)
        {
            config.Container = new ContainerConfig
            {
                BaseImage = node["container"]!["baseImage"]?.GetValue<string>()
                    ?? "mcr.microsoft.com/dotnet/aspnet:10.0",
                MemoryMb = node["container"]!["memoryMb"]?.GetValue<int>() ?? 256,
                CpuLimit = node["container"]!["cpuLimit"]?.GetValue<string>() ?? "0.5"
            };

            if (node["container"]!["environment"] is JsonObject envObj)
            {
                config.Container.Environment = envObj
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.GetValue<string>() ?? "");
            }

            if (node["container"]!["volumes"] is JsonArray volumesArray)
            {
                config.Container.Volumes = volumesArray
                    .Select(v => v?.GetValue<string>() ?? "")
                    .ToList();
            }
        }

        return config;
    }

    private PluginPersistenceConfig ParsePersistence(JsonNode node)
    {
        var config = new PluginPersistenceConfig
        {
            Mode = node["mode"]?.GetValue<string>() ?? "schema",
            SchemaName = node["schemaName"]?.GetValue<string>(),
            Connection = node["connection"]?.GetValue<string>() ?? "HostDefault"
        };

        if (node["migrations"] != null)
        {
            config.Migrations = new MigrationConfig
            {
                Strategy = node["migrations"]!["strategy"]?.GetValue<string>() ?? "plugin-owned",
                AutoApplyOnInstall = node["migrations"]!["autoApplyOnInstall"]?.GetValue<bool>() ?? true
            };
        }

        return config;
    }

    private PluginMultitenancyConfig ParseMultitenancy(JsonNode node)
    {
        var config = new PluginMultitenancyConfig
        {
            Mode = node["mode"]?.GetValue<string>() ?? "row",
            TenantKey = node["tenantKey"]?.GetValue<string>() ?? "TenantId"
        };

        if (node["enforcement"] != null)
        {
            config.Enforcement = new TenantEnforcementConfig
            {
                RequireTenantHeader = node["enforcement"]!["requireTenantHeader"]?.GetValue<bool>() ?? true,
                TenantHeader = node["enforcement"]!["tenantHeader"]?.GetValue<string>() ?? "X-Tenant-Id"
            };

            if (node["enforcement"]!["applyToEntities"] is JsonArray entitiesArray)
            {
                config.Enforcement.ApplyToEntities = entitiesArray
                    .Select(e => e?.GetValue<string>() ?? "")
                    .ToList();
            }
        }

        return config;
    }

    private PluginAuthConfig ParseAuth(JsonNode node)
    {
        var config = new PluginAuthConfig
        {
            DefaultScheme = node["defaultScheme"]?.GetValue<string>() ?? "Bearer"
        };

        if (node["defaultScopes"] is JsonArray scopesArray)
        {
            config.DefaultScopes = scopesArray.Select(s => s?.GetValue<string>() ?? "").ToList();
        }

        return config;
    }

    private EntityPersistenceConfig ParseEntityPersistence(JsonNode node)
    {
        var config = new EntityPersistenceConfig
        {
            Kind = node["kind"]?.GetValue<string>() ?? "entity",
            Table = node["table"]?.GetValue<string>()
        };

        if (node["key"] != null)
        {
            config.Key = new KeyConfig
            {
                Strategy = node["key"]!["strategy"]?.GetValue<string>() ?? "guid",
                Property = node["key"]!["property"]?.GetValue<string>() ?? "Id",
                Generated = node["key"]!["generated"]?.GetValue<string>() ?? "server"
            };

            if (node["key"]!["properties"] is JsonArray propsArray)
            {
                config.Key.Properties = propsArray.Select(p => p?.GetValue<string>() ?? "").ToList();
            }
        }

        if (node["concurrency"] != null)
        {
            config.Concurrency = new ConcurrencyConfig
            {
                Mode = node["concurrency"]!["mode"]?.GetValue<string>() ?? "none",
                Property = node["concurrency"]!["property"]?.GetValue<string>()
            };
        }

        if (node["softDelete"] != null)
        {
            config.SoftDelete = new SoftDeleteConfig
            {
                Enabled = node["softDelete"]!["enabled"]?.GetValue<bool>() ?? false,
                Property = node["softDelete"]!["property"]?.GetValue<string>() ?? "IsDeleted"
            };
        }

        if (node["indexes"] is JsonArray indexesArray)
        {
            config.Indexes = indexesArray
                .Where(i => i != null)
                .Select(i => new IndexConfig
                {
                    Name = i!["name"]?.GetValue<string>() ?? "",
                    Unique = i["unique"]?.GetValue<bool>() ?? false,
                    Filter = i["filter"]?.GetValue<string>(),
                    Properties = (i["properties"] as JsonArray)?
                        .Select(p => p?.GetValue<string>() ?? "")
                        .ToList() ?? []
                })
                .ToList();
        }

        if (node["relationships"] is JsonArray relsArray)
        {
            config.Relationships = relsArray
                .Where(r => r != null)
                .Select(r => new RelationshipConfig
                {
                    Name = r!["name"]?.GetValue<string>() ?? "",
                    Type = r["type"]?.GetValue<string>() ?? "many-to-one",
                    Target = r["target"]?.GetValue<string>() ?? "",
                    ForeignKey = r["foreignKey"]?.GetValue<string>(),
                    Required = r["required"]?.GetValue<bool>() ?? false,
                    Cascade = r["cascade"]?.GetValue<string>(),
                    OnDelete = r["onDelete"]?.GetValue<string>()
                })
                .ToList();
        }

        return config;
    }

    private ValidationConfig ParseValidation(JsonNode node)
    {
        var config = new ValidationConfig();

        if (node["rules"] is JsonArray rulesArray)
        {
            config.Rules = rulesArray
                .Where(r => r != null)
                .Select(r => new ValidationRule
                {
                    Rule = r!["rule"]?.GetValue<string>() ?? "",
                    Field = r["field"]?.GetValue<string>(),
                    Message = r["message"]?.GetValue<string>(),
                    Pattern = r["pattern"]?.GetValue<string>(),
                    Min = r["min"]?.GetValue<int>(),
                    Max = r["max"]?.GetValue<int>(),
                    Fields = (r["fields"] as JsonArray)?
                        .Select(f => f?.GetValue<string>() ?? "")
                        .ToList()
                })
                .ToList();
        }

        return config;
    }

    private Dictionary<string, PropertyDefinition> ParseProperties(JsonNode node)
    {
        var properties = new Dictionary<string, PropertyDefinition>();

        foreach (var (name, prop) in node.AsObject())
        {
            if (prop == null) continue;

            var propDef = new PropertyDefinition
            {
                Name = name,
                Type = prop["type"]?.GetValue<string>() ?? "string",
                Format = prop["format"]?.GetValue<string>(),
                Nullable = prop["nullable"]?.GetValue<bool>() ?? false,
                ReadOnly = prop["readOnly"]?.GetValue<bool>() ?? false,
                MinLength = prop["minLength"]?.GetValue<int>(),
                MaxLength = prop["maxLength"]?.GetValue<int>(),
                Default = prop["default"]?.ToString()
            };

            // Parse x-tenant
            if (prop["x-tenant"] != null)
            {
                propDef.Tenant = new TenantPropertyConfig
                {
                    Required = prop["x-tenant"]!["required"]?.GetValue<bool>() ?? true,
                    Source = prop["x-tenant"]!["source"]?.GetValue<string>() ?? "header"
                };
            }

            properties[name] = propDef;
        }

        return properties;
    }

    private EndpointAuthConfig ParseEndpointAuth(JsonNode node)
    {
        var config = new EndpointAuthConfig
        {
            AllowAnonymous = node["allowAnonymous"]?.GetValue<bool>() ?? false
        };

        if (node["scopes"] is JsonArray scopesArray)
        {
            config.Scopes = scopesArray.Select(s => s?.GetValue<string>() ?? "").ToList();
        }

        if (node["roles"] is JsonArray rolesArray)
        {
            config.Roles = rolesArray.Select(r => r?.GetValue<string>() ?? "").ToList();
        }

        return config;
    }

    private ParameterDefinition ParseParameter(JsonNode node)
    {
        return new ParameterDefinition
        {
            Name = node["name"]?.GetValue<string>() ?? "",
            In = node["in"]?.GetValue<string>() ?? "query",
            Required = node["required"]?.GetValue<bool>() ?? false,
            Schema = node["schema"]?.ToJsonString() ?? "{}"
        };
    }

    private RequestBodyDefinition ParseRequestBody(JsonNode node)
    {
        var body = new RequestBodyDefinition
        {
            Required = node["required"]?.GetValue<bool>() ?? false
        };

        var content = node["content"]?["application/json"]?["schema"];
        if (content != null)
        {
            body.SchemaRef = content["$ref"]?.GetValue<string>();
            body.Schema = content.ToJsonString();
        }

        return body;
    }

    private Dictionary<string, ResponseDefinition> ParseResponses(JsonNode node)
    {
        var responses = new Dictionary<string, ResponseDefinition>();

        foreach (var (statusCode, response) in node.AsObject())
        {
            if (response == null) continue;

            var respDef = new ResponseDefinition
            {
                StatusCode = statusCode,
                Description = response["description"]?.GetValue<string>()
            };

            var content = response["content"]?["application/json"]?["schema"];
            if (content != null)
            {
                respDef.SchemaRef = content["$ref"]?.GetValue<string>();
                respDef.Schema = content.ToJsonString();
            }

            responses[statusCode] = respDef;
        }

        return responses;
    }
}

/// <summary>
/// Represents a full entity definition from OpenAPI schema.
/// </summary>
public class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "object";
    public EntityPersistenceConfig? Persistence { get; set; }
    public ValidationConfig? Validation { get; set; }
    public List<string> RequiredProperties { get; set; } = [];
    public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();
}

/// <summary>
/// Represents a property definition from OpenAPI schema.
/// </summary>
public class PropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? Format { get; set; }
    public bool Nullable { get; set; }
    public bool ReadOnly { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Default { get; set; }
    public TenantPropertyConfig? Tenant { get; set; }
}

/// <summary>
/// Represents an endpoint definition from OpenAPI paths.
/// </summary>
public class EndpointDefinition
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? OperationId { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public EndpointAuthConfig? Auth { get; set; }
    public List<ParameterDefinition> Parameters { get; set; } = [];
    public RequestBodyDefinition? RequestBody { get; set; }
    public Dictionary<string, ResponseDefinition> Responses { get; set; } = new();
}

/// <summary>
/// Represents a parameter definition.
/// </summary>
public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string In { get; set; } = "query";
    public bool Required { get; set; }
    public string Schema { get; set; } = "{}";
}

/// <summary>
/// Represents a request body definition.
/// </summary>
public class RequestBodyDefinition
{
    public bool Required { get; set; }
    public string? SchemaRef { get; set; }
    public string? Schema { get; set; }
}

/// <summary>
/// Represents a response definition.
/// </summary>
public class ResponseDefinition
{
    public string StatusCode { get; set; } = "200";
    public string? Description { get; set; }
    public string? SchemaRef { get; set; }
    public string? Schema { get; set; }
}
