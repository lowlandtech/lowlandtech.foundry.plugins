# Examples

Demonstration plugins that show how to use the `IPlugin` architecture. **Delete these for production** or use them as starting points for your own plugins.

## Projects

| Project | Description |
|---------|-------------|
| **SamplePlugin** | `IPlugin` with `MenuFeature` - pages, submenus, custom layouts |
| **PremiumTheme** | `IPlugin` with `ThemeFeature` - custom color palettes |

Both implement the new `IPlugin` interface with lifecycle management and individually toggleable features.

## SamplePlugin

Demonstrates navigation plugins with `MenuFeature`:

### Plugin Implementation

```csharp
public class SamplePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.sampleplugin",
        Name: "Sample Plugin",
        Description: "Demo plugin with navigation pages",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["sample", "demo"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new MenuFeature(this, "navigation",
            "Navigation Menu", "Menu items", DiscoverMenuItems());
    }
}
```

### Menu Registration (on pages)

```csharp
@page "/dashboard"
@attribute [MenuItem(
    Title = "Dashboard",
    Icon = Icons.Material.Filled.Dashboard,
    Location = MenuLocation.Sidebar,
    Order = 10
)]
```

### Submenu Hierarchy

```csharp
// Parent menu item
@attribute [MenuItem(Title = "Reports", Icon = Icons.Material.Filled.Assessment)]

// Child items
@attribute [MenuItem(Title = "Sales", ParentMenu = "Reports", Order = 21)]
@attribute [MenuItem(Title = "Inventory", ParentMenu = "Reports", Order = 22)]
```

### Custom Layouts

```csharp
@attribute [PageLayout(typeof(FullScreenLayout))]
```

### Pages Included

- `Dashboard.razor` - Basic sidebar page
- `Reports.razor` - Parent menu with children
- `SalesReport.razor` - Submenu child
- `InventoryReport.razor` - Submenu child
- `Settings.razor` - Topbar page
- `FullScreen.razor` - Custom layout example

## PremiumTheme

Demonstrates theme plugins with `ThemeFeature`:

### Plugin Implementation

```csharp
public class PremiumThemePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.premiumtheme",
        Name: "Premium Theme Pack",
        Description: "Additional premium themes",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["themes", "premium"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(this, "amethyst", "Amethyst Theme",
            "Purple-based theme", new AmethystTheme());
        yield return new ThemeFeature(this, "midnight-gold", "Midnight Gold",
            "Gold accent theme", new MidnightGoldTheme());
        yield return new ThemeFeature(this, "rose-gold", "Rose Gold",
            "Pink/rose theme", new RoseGoldTheme());
    }
}
```

### Creating a Theme

```csharp
public class AmethystTheme : ThemeBase
{
    public override string Name => "amethyst";
    public override string DisplayName => "Amethyst (Premium)";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#7c3aed",
        Secondary = "#a855f7",
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#a78bfa",
        Secondary = "#c084fc",
    };
}
```

### Themes Included

- `AmethystTheme` - Purple/violet palette
- `MidnightGoldTheme` - Dark with gold accents
- `RoseGoldTheme` - Warm rose with copper highlights

## Using These as Templates

### Creating Your Own Plugin

1. Copy `SamplePlugin` folder
2. Rename to `YourCompany.YourProduct.YourPlugin`
3. Update namespace in all files
4. Update `PluginMetadata` in the plugin class
5. Replace features in `CreateFeatures()` with your own
6. Replace pages with your actual features
7. Add to `Plugins.slnx`
8. Register in Host: `options.AddAssemblyOf<YourPlugin>()`

### Creating Your Own Theme Pack

1. Copy `PremiumTheme` folder
2. Rename appropriately
3. Update `PluginMetadata` in the plugin class
4. Replace theme classes with your brand colors
5. Update `ThemeFeature` yields in `CreateFeatures()`
6. Add to `Plugins.slnx`
7. Register in Host: `options.AddAssemblyOf<YourThemePlugin>()`

## Plugin Registration

Both plugins are registered in Host's `Program.cs`:

```csharp
builder.Services.AddPlugins(options =>
{
    options.AddAssemblyOf<SamplePlugin>();
    options.AddAssemblyOf<PremiumThemePlugin>();
});
```

This enables:
- Automatic discovery on startup
- Lifecycle management (install, activate, disable)
- Feature-level enable/disable
- State persistence across restarts

## Removing Examples

If you don't need these anymore:

```bash
# Remove from solution
dotnet sln Plugins.slnx remove src/examples/LowlandTech.Foundry.SamplePlugin
dotnet sln Plugins.slnx remove src/examples/LowlandTech.Foundry.PremiumTheme

# Delete the folder
rm -rf src/examples

# Update Host's Program.cs - remove AddAssemblyOf<> for these plugins
```

Or just delete the entire `src/examples/` folder and update the solution file.
