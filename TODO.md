# Plugin System TODO

This document tracks the remaining work for the plugin system spike. The core architecture is complete and ready to merge into the main repository where existing code generators can be leveraged.

## Completed Work ✅

### Database Entities
- [x] `PluginHostingSettings` - Global settings for Container/InProcess/Desktop modes
- [x] `InstalledPluginPackage` - Tracks installed plugins with manifest, container IDs, schema names
- [x] Updated `ApplicationDbContext` with new DbSets and configurations

### Plugin Manifest Models (PluginCore/Manifest/)
- [x] `PluginManifest.cs` - C# representation of `x-plugin` OpenAPI extension
- [x] `EntitySchema.cs` - Models for `x-persistence`, `x-validation`, `x-tenant`, `x-auth`
- [x] `OpenApiManifestParser.cs` - Parses OpenAPI JSON and extracts all vendor extensions

### Plugin Host Abstraction (PluginCore/Hosting/)
- [x] `IPluginHost` - Interface for Install/Uninstall/Activate/Deactivate/Health
- [x] `IPluginPackageInstaller` - Interface for package installation from NuGet feeds
- [x] Result records: `PluginInstallResult`, `PluginActivationResult`, `PluginHealthResult`, etc.

### Host Implementations (Api/Services/)
- [x] `ContainerPluginHost` - Docker-based hosting with:
  - Schema creation/deletion per plugin
  - Container lifecycle management (create, start, stop, remove)
  - Health checking via HTTP
  - Image building placeholders
- [x] `InProcessPluginHost` - AssemblyLoadContext-based hosting with:
  - Isolated assembly loading (collectible)
  - Schema creation/deletion
  - Runtime plugin activation/deactivation
- [x] `PluginHostFactory` - Factory to select host based on mode

### Service Registration (Api/Extensions/)
- [x] `PluginHostingExtensions.cs` - `AddPluginHosting()` for DI registration

## Remaining Work 🔧

### 1. Code Generator (integrate with main repo generator)

The OpenAPI manifest contains all information needed to generate:

```
From x-persistence on schemas:
├── DbContext with schema-per-plugin
│   ├── Entity classes from components/schemas where kind="entity"
│   ├── Owned types where kind="owned"
│   ├── Indexes from x-persistence.indexes
│   ├── Relationships from x-persistence.relationships
│   └── Soft delete global query filters
│
├── FluentValidation validators from x-validation
│   ├── NotEmpty, Email, UUID rules
│   ├── AtLeastOneOf for patch DTOs
│   └── Custom regex patterns
│
└── Minimal API endpoints from paths
    ├── Route handlers with proper HTTP methods
    ├── Authorization from x-auth (scopes, roles)
    ├── Tenant header extraction from x-tenant
    └── Request/response mapping
```

**Key files to generate per plugin:**
- `{PluginId}DbContext.cs` - EF Core context with schema configuration
- `Entities/*.cs` - Entity classes from OpenAPI schemas
- `Validators/*.cs` - FluentValidation validators
- `Endpoints/*.cs` - Minimal API endpoint classes
- `Program.cs` - Plugin API startup
- `Dockerfile` - Container image definition

### 2. YARP Reverse Proxy Gateway

Configure YARP to route plugin requests:

```csharp
// In Api Program.cs
builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters);

// Dynamic route configuration from InstalledPluginPackages
// Route: /api/plugins/{pluginId}/* → http://plugin-container:{port}/*
```

**Implementation needed:**
- `PluginProxyConfigProvider` - IProxyConfigProvider that reads from database
- Dynamic cluster/route updates when plugins activate/deactivate
- Health check integration with YARP

### 3. NuGet Feed for Plugin Packages

Simplified BaGet-inspired feed:

```
Endpoints needed:
├── GET  /v3/index.json              → Service index
├── GET  /v3/search                  → Search packages
├── GET  /v3/registration/{id}/*     → Package metadata
├── GET  /v3/package/{id}/{ver}/*    → Package content
├── PUT  /api/v2/package             → Upload package
└── DELETE /api/v2/package/{id}/{ver} → Delete package
```

**Storage:**
- File system or Azure Blob for .nupkg files
- Database for package metadata and search index

### 4. Plugin Manager UI (Host/Components/)

WordPress-style plugin management:

```
Pages needed:
├── /admin/plugins                   → Installed plugins list
│   ├── Activate/Deactivate buttons
│   ├── Health status indicators
│   ├── Version info
│   └── Uninstall option
│
├── /admin/plugins/add               → Browse available plugins
│   ├── Search by name/tag
│   ├── Plugin cards with screenshots
│   ├── Install button
│   └── Version selector
│
├── /admin/plugins/{id}              → Plugin details
│   ├── Full description
│   ├── Features list (toggleable)
│   ├── Configuration
│   └── Logs/health history
│
└── /admin/plugins/settings          → Global plugin settings
    ├── Default hosting mode
    ├── Feed URLs
    ├── Docker settings
    └── Multitenancy config
```

### 5. Container Image Builder

Complete the `BuildPluginImageAsync` implementation:

```csharp
// Generate Dockerfile from template
// Copy plugin assemblies and dependencies
// Build image using Docker.DotNet
// Push to configured registry (optional)
```

### 6. Database Migrations

Create EF Core migration for new entities:

```bash
dotnet ef migrations add AddPluginHosting -p src/backend/LowlandTech.Foundry.Api
```

## Architecture Decisions

### Hosting Modes

| Mode | Use Case | Database | Isolation |
|------|----------|----------|-----------|
| **Container** | Enterprise, microservices | Schema per plugin | Full process isolation |
| **InProcess** | Small backend, mobile | Schema per plugin | AssemblyLoadContext |
| **Desktop** | Photino/MAUI | SQLite per plugin | AssemblyLoadContext |

### Plugin Lifecycle

```
┌──────────────┐
│  Discovered  │ ← Package found in feed
└──────┬───────┘
       │ InstallAsync()
       ▼
┌──────────────┐
│  Installed   │ ← Schema created, migrations applied
└──────┬───────┘
       │ ActivateAsync()
       ▼
┌──────────────┐
│  Activated   │ ← Container running or assembly loaded
└──────┬───────┘
       │ DeactivateAsync()
       ▼
┌──────────────┐
│   Disabled   │ ← Container stopped, can reactivate
└──────────────┘
       │ UninstallAsync(removeData: true)
       ▼
┌──────────────┐
│   Removed    │ ← Schema dropped, package deleted
└──────────────┘
```

### OpenAPI x-plugin Extension Reference

```yaml
x-plugin:
  id: "vendor.pluginname"
  displayName: "Plugin Name"
  version: "1.0.0"
  hostApiCompatibility: "^1.0.0"

  runtime:
    hosting: "container"              # container | inprocess
    routeBase: "/api/plugins/vendor.pluginname"
    healthPath: "/health"
    openApiPath: "/openapi.json"
    container:
      baseImage: "mcr.microsoft.com/dotnet/aspnet:10.0"
      memoryMb: 256
      cpuLimit: "0.5"

  persistence:
    mode: "schema"                    # schema | database
    schemaName: "plug_vendor_pluginname"
    connection: "HostDefault"
    migrations:
      strategy: "plugin-owned"
      autoApplyOnInstall: true

  multitenancy:
    mode: "row"                       # none | row | schema | database
    tenantKey: "TenantId"
    enforcement:
      applyToEntities: ["Entity1", "Entity2"]
      requireTenantHeader: true
      tenantHeader: "X-Tenant-Id"

  auth:
    defaultScheme: "Bearer"
    defaultScopes: ["plugin.read"]
```

### Schema x-persistence Extension Reference

```yaml
components:
  schemas:
    Customer:
      type: object
      x-persistence:
        kind: "entity"                # entity | owned | projection | request
        table: "customers"
        key:
          strategy: "guid"            # guid | int | composite
          property: "CustomerId"
          generated: "server"
        concurrency:
          mode: "rowversion"
          property: "RowVersion"
        softDelete:
          enabled: true
          property: "IsDeleted"
        indexes:
          - name: "ix_customers_email"
            unique: true
            properties: ["Email"]
            filter: "IsDeleted = false"
        relationships:
          - name: "Contacts"
            type: "one-to-many"
            target: "Contact"
            foreignKey: "CustomerId"
            cascade: "delete"

      x-validation:
        rules:
          - rule: "notEmpty"
            field: "Name"
            message: "Name is required."
          - rule: "email"
            field: "Email"

      properties:
        TenantId:
          type: string
          format: uuid
          x-tenant:
            required: true
            source: "header"
```

## File Structure After Merge

```
src/
├── frontend/
│   └── LowlandTech.Foundry.PluginCore/
│       ├── Abstractions/          # IPlugin, IPluginFeature, etc.
│       ├── Features/              # ThemeFeature, MenuFeature
│       ├── Hosting/               # IPluginHost, IPluginPackageInstaller
│       ├── Manifest/              # OpenAPI extension models, parser
│       └── Services/              # PluginManager, state stores
│
├── backend/
│   └── LowlandTech.Foundry.Api/
│       ├── Data/
│       │   ├── PluginHostingSettings.cs
│       │   ├── InstalledPluginPackage.cs
│       │   └── ApplicationDbContext.cs
│       ├── Services/
│       │   ├── ContainerPluginHost.cs
│       │   ├── InProcessPluginHost.cs
│       │   ├── PluginHostFactory.cs
│       │   └── DatabasePluginStateStore.cs
│       └── Extensions/
│           └── PluginHostingExtensions.cs
│
└── tools/
    └── PluginGenerator/           # T4 or Roslyn source generator
        ├── Templates/
        │   ├── DbContext.tt
        │   ├── Entity.tt
        │   ├── Validator.tt
        │   ├── Endpoint.tt
        │   └── Dockerfile.tt
        └── PluginProjectGenerator.cs
```

## Integration with Main Repo

1. Copy `PluginCore/Manifest/` and `PluginCore/Hosting/` to main repo
2. Copy `Api/Data/PluginHostingSettings.cs` and `InstalledPluginPackage.cs`
3. Copy `Api/Services/*PluginHost.cs` files
4. Integrate with existing code generator for:
   - DbContext generation from `x-persistence`
   - Endpoint generation from OpenAPI paths
   - Validator generation from `x-validation`
5. Add YARP configuration to API
6. Add Plugin Manager UI to Host

## Notes

- The spike validates the architecture: plugins as isolated microservices with their own schemas
- OpenAPI + vendor extensions provide a complete contract for code generation
- Container mode is preferred for enterprise; InProcess for development/small deployments
- The existing generator in the main repo can handle most of the code generation work
- Focus on YARP integration first for the MVP
