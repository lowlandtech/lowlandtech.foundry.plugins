# LowlandTech.Foundry.PluginCore

The plugin framework foundation - defines the contracts and services that enable plugin lifecycle management, feature discovery, menu registration, and theming.

## What This Project Does

PluginCore provides:

- **Plugin Architecture**: `IPlugin`, `IPluginFeature`, `IPluginManager` for full lifecycle management
- **Feature Types**: `ThemeFeature`, `MenuFeature` for common plugin capabilities
- **State Persistence**: `IPluginStateStore` for persisting plugin/feature enabled states
- **Plugin Loading Infrastructure**: Assembly, folder, and NuGet-based plugin catalogs with isolated load contexts
- **Menu Discovery**: Attribute-based menu registration (`[MenuItem]`) and dynamic menu provider services
- **Theming System**: `ITheme` interface and `ThemeService` for runtime theme switching
- **Layout Control**: `[PageLayout]` attribute for per-page layout customization

## Plugin Architecture

### Core Interfaces

| Interface | Purpose |
|-----------|---------|
| `IPlugin` | Core plugin contract with metadata, lifecycle methods, and features |
| `IPluginFeature` | Individual capability within a plugin that can be enabled/disabled |
| `IPluginManager` | Manages plugin discovery, installation, activation, and feature toggling |
| `IPluginStateStore` | Persists plugin and feature state across restarts |

### Plugin Lifecycle

```
Discovered → Installed → Activated ↔ Disabled
                ↓
              Error
```

- **Discovered**: Plugin type found but not yet set up
- **Installed**: Plugin initialized, can register services
- **Activated**: Plugin running, features available
- **Disabled**: Plugin temporarily deactivated
- **Error**: Plugin failed to load or activate

### Base Classes

| Class | Purpose |
|-------|---------|
| `PluginBase` | Abstract base with lazy feature creation via `CreateFeatures()` |
| `PluginFeatureBase` | Abstract base for custom features with enable/disable support |

### Built-in Features

| Feature | Purpose |
|---------|---------|
| `ThemeFeature` | Provides themes to the theme service |
| `MenuFeature` | Provides navigation menu items |

## Key Directories

```
Abstractions/          # Core interfaces and base classes
├── IPlugin.cs         # Plugin contract
├── IPluginFeature.cs  # Feature contract
├── IPluginManager.cs  # Manager contract
├── PluginBase.cs      # Abstract plugin base
├── PluginFeatureBase.cs # Abstract feature base
├── PluginMetadata.cs  # Plugin metadata record
└── PluginState.cs     # Lifecycle state enum

Features/              # Built-in feature implementations
├── IThemeFeature.cs   # Theme feature interface
├── ThemeFeature.cs    # Theme feature implementation
├── IMenuFeature.cs    # Menu feature interface
└── MenuFeature.cs     # Menu feature implementation

Services/              # Plugin management services
├── IPluginStateStore.cs    # State persistence interface
├── InMemoryPluginStateStore.cs # In-memory state store
└── PluginManager.cs        # Plugin lifecycle manager

Extensions/            # DI registration and helpers
├── ServiceCollectionExtensions.cs # AddPlugins(), AddPluginStateStore<T>()
└── PluginExtensions.cs     # Query helpers (GetThemes, GetMenuItems, etc.)

Catalogs/              # Assembly discovery
├── IPluginCatalog.cs       # Catalog contract
├── AssemblyPluginCatalog.cs # Direct assembly loading
├── FolderPluginCatalog.cs   # Folder scanning
└── NuGetPluginCatalog.cs    # NuGet package loading

Theming/               # Theme system
├── ITheme.cs          # Theme contract
├── ThemeBase.cs       # Abstract theme base
├── ThemeService.cs    # Runtime theme management
└── DefaultTheme.cs    # Built-in default theme

Attributes/            # Convention-based discovery
├── MenuItemAttribute.cs    # Page menu registration
└── PageLayoutAttribute.cs  # Per-page layout override
```

## Usage

### Creating a Plugin

```csharp
public class MyPlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "mycompany.myplugin",
        Name: "My Plugin",
        Description: "Does useful things",
        Version: new Version(1, 0, 0),
        Author: "My Company",
        Tags: ["utility"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(this, "dark", "Dark Theme",
            "A dark color scheme", new DarkTheme());
        yield return new MenuFeature(this, "nav", "Navigation",
            "Menu items", myMenuItems);
    }
}
```

### Registering Plugins

```csharp
// In Program.cs
builder.Services.AddPlugins(options =>
{
    options.AddAssemblyOf<MyPlugin>();
    options.AutoDiscoverOnStartup = true;
    options.AutoActivateOnStartup = true;
});
```

### Injecting Plugins

```csharp
public class MyService
{
    public MyService(IPluginManager pluginManager, IEnumerable<IPlugin> plugins)
    {
        // Query all active themes
        var themes = plugins.GetActiveThemes();

        // Toggle a feature
        await pluginManager.EnableFeatureAsync("mycompany.myplugin", "dark");
    }
}
```

## Why It's Standalone

**This project MUST remain separate.** Here's why:

1. **Shared Contract**: Both the Host and plugins reference this assembly. If you merged it into a "Core" project containing Host-specific code, every plugin would pull in web server dependencies they don't need.

2. **NuGet Distribution**: This is designed to be published as a NuGet package. Plugin authors only need `LowlandTech.Foundry.PluginCore` - they shouldn't have to reference your entire application.

3. **Minimal Dependencies**: Notice the package references - just `MudBlazor` for theming types and `NuGet.Protocol` for the NuGet catalog. No web server, no database, no P2P. Keep it that way.

4. **Platform Agnostic**: The plugin system works with ASP.NET, Photino (desktop), and MAUI (mobile). No `IApplicationBuilder` or ASP.NET-specific types.

5. **Version Independence**: You can evolve your Host, API, and P2P systems without forcing plugin authors to update. As long as PluginCore's contracts remain stable, plugins keep working.

## Template Customization

When using this repo as a template:

- **Keep this project** - it's the foundation of the plugin architecture
- Add your own feature types (extend `PluginFeatureBase`)
- Add your own attributes to the `Attributes/` folder
- Extend `ITheme` if you need additional theme properties
- Create new `IPluginCatalog` implementations for custom loading strategies

## Should You Merge It?

**No.** This is one of the few projects in the solution that genuinely needs to be standalone. Merging it would:

- Force plugin authors to reference unnecessary dependencies
- Make it harder to version the plugin API independently
- Violate the principle that shared contracts should be minimal
