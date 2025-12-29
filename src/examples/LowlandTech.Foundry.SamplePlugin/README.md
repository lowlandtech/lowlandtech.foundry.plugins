# LowlandTech.Foundry.SamplePlugin

A demonstration plugin - shows how to implement `IPlugin` with menu features, submenus, and custom page layouts.

## What This Project Does

SamplePlugin demonstrates the plugin system:

- **IPlugin Implementation**: `SamplePlugin.cs` extends `PluginBase` with metadata and features
- **MenuFeature**: Provides navigation items discovered from pages with `[MenuItem]`
- **Submenu Support**: Shows nested menu structures (Reports → Sales, Inventory)
- **Custom Layouts**: Uses `[PageLayout]` for the FullScreen page
- **Basic Pages**: Dashboard, Reports, Settings examples

## Plugin Implementation

```csharp
public class SamplePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.sampleplugin",
        Name: "Sample Plugin",
        Description: "Demo plugin with navigation pages",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["sample", "demo", "navigation"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        // Discovers pages with [MenuItem] in this assembly
        var menuItems = DiscoverMenuItems();
        yield return new MenuFeature(this, "navigation", "Navigation Menu",
            "Plugin menu items", menuItems);
    }
}
```

## Why It's Standalone

**This is intentionally a separate assembly.** Here's why:

1. **Plugin Demonstration**: The whole point is to show plugins as separate DLLs with `IPlugin` implementation.

2. **Template Example**: When someone uses this repo as a template, they can look at SamplePlugin to understand how to create their own plugins.

3. **Optional Content**: In a real app, you'd remove this and replace with your actual plugins. Having it separate makes removal trivial.

4. **Lifecycle Testing**: The Host discovers this via `AddPlugins()` and manages its lifecycle (install, activate, etc.).

## Template Customization

When using this repo as a template:

**Learning from it:**
- Study `SamplePlugin.cs` to see how `IPlugin` works
- See how `MenuFeature` discovers pages with `[MenuItem]`
- Understand the lifecycle methods in `PluginBase`

**Creating your own plugins:**
1. Copy SamplePlugin as a starting point
2. Rename the project and namespace
3. Update `PluginMetadata` with your info
4. Modify the pages for your features
5. Add your own `IPluginFeature` implementations

**Removing it:**
- Delete the project
- Remove the reference from Host's `Program.cs`
- The plugin system still works - just no sample content

## Key Files

| File | Purpose |
|------|---------|
| `SamplePlugin.cs` | `IPlugin` implementation with `MenuFeature` |
| `Pages/Dashboard.razor` | Basic sidebar page example |
| `Pages/Reports.razor` | Page with submenu children |
| `Pages/SalesReport.razor` | Submenu child page |
| `Pages/InventoryReport.razor` | Submenu child page |
| `Pages/Settings.razor` | Topbar page example |
| `Pages/FullScreen.razor` | Custom layout example |

## Menu Registration

Pages use `[MenuItem]` attributes:

```csharp
@page "/dashboard"
@attribute [MenuItem(
    Title = "Dashboard",
    Icon = Icons.Material.Filled.Dashboard,
    Location = MenuLocation.Sidebar,
    Order = 10
)]
```

Submenus:
```csharp
@attribute [MenuItem(Title = "Sales Report", ParentMenu = "Reports", Order = 21)]
```

## Dependencies

- `MudBlazor` - UI framework
- `PluginCore` - Plugin framework (`IPlugin`, `PluginBase`, `MenuFeature`, attributes)

## Should You Merge It?

**Definitely not.**

The entire purpose of this project is to be a separate assembly that demonstrates the plugin system. Merging it would defeat its purpose.

However, you should **delete it** when building a real application. It's example content, not production code. Replace it with your actual plugins.

**What to do:**
1. Keep it while learning the plugin system
2. Use it as a reference when building your plugins
3. Delete it before shipping your app
4. Or keep it and rename it to your first actual plugin
