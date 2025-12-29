# LowlandTech.Foundry.PluginCore

The plugin framework foundation - defines the contracts and services that enable dynamic plugin loading, menu discovery, and theming.

## What This Project Does

PluginCore provides:

- **Plugin Loading Infrastructure**: Assembly, folder, and NuGet-based plugin catalogs with isolated load contexts
- **Menu Discovery**: Attribute-based menu registration (`[MenuItem]`) and dynamic menu provider services
- **Theming System**: `ITheme` interface and `ThemeService` for runtime theme switching
- **Layout Control**: `[PageLayout]` attribute for per-page layout customization

## Why It's Standalone

**This project MUST remain separate.** Here's why:

1. **Shared Contract**: Both the Host and plugins reference this assembly. If you merged it into a "Core" project containing Host-specific code, every plugin would pull in web server dependencies they don't need.

2. **NuGet Distribution**: This is designed to be published as a NuGet package. Plugin authors only need `LowlandTech.Foundry.PluginCore` - they shouldn't have to reference your entire application.

3. **Minimal Dependencies**: Notice the package references - just `MudBlazor` for theming types and `NuGet.Protocol` for the NuGet catalog. No web server, no database, no P2P. Keep it that way.

4. **Version Independence**: You can evolve your Host, API, and P2P systems without forcing plugin authors to update. As long as PluginCore's contracts remain stable, plugins keep working.

## Template Customization

When using this repo as a template:

- **Keep this project** - it's the foundation of the plugin architecture
- Add your own attributes to the `Attributes/` folder
- Extend `ITheme` if you need additional theme properties
- Create new `IPluginCatalog` implementations for custom loading strategies (cloud storage, git repos, etc.)

## Key Files

| File | Purpose |
|------|---------|
| `Attributes/MenuItemAttribute.cs` | Marks Razor components as menu items |
| `Attributes/PageLayoutAttribute.cs` | Specifies custom layouts per page |
| `Catalogs/IPluginCatalog.cs` | Contract for plugin discovery |
| `Catalogs/AssemblyPluginCatalog.cs` | Loads plugins from compiled assemblies |
| `Catalogs/FolderPluginCatalog.cs` | Scans directories for plugin DLLs |
| `Catalogs/NuGetPluginCatalog.cs` | Downloads plugins from NuGet feeds |
| `Theming/ITheme.cs` | Theme contract with color definitions |
| `Theming/ThemeService.cs` | Runtime theme management |

## Should You Merge It?

**No.** This is one of the few projects in the solution that genuinely needs to be standalone. Merging it would:

- Force plugin authors to reference unnecessary dependencies
- Make it harder to version the plugin API independently
- Violate the principle that shared contracts should be minimal
