# Examples

Demonstration plugins that show how to use the plugin system. **Delete these for production** or use them as starting points for your own plugins.

## Projects

| Project | Description |
|---------|-------------|
| **SamplePlugin** | Example pages with menu items, submenus, and custom layouts |
| **PremiumTheme** | Example theme plugin with custom color palettes |

## SamplePlugin

Demonstrates the core plugin features:

### Menu Registration

```csharp
@page "/dashboard"
@attribute [MenuItem(
    Title = "Dashboard",
    Icon = Icons.Material.Filled.Dashboard,
    Location = MenuLocation.Sidebar,
    Order = 100
)]
```

### Submenu Hierarchy

```csharp
// Parent menu item
@attribute [MenuItem(Title = "Reports", Icon = Icons.Material.Filled.Assessment)]

// Child items
@attribute [MenuItem(Title = "Sales", ParentMenu = "Reports", Order = 1)]
@attribute [MenuItem(Title = "Inventory", ParentMenu = "Reports", Order = 2)]
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
- `Settings.razor` - Another sidebar page
- `FullScreen.razor` - Custom layout example

## PremiumTheme

Demonstrates the theming system:

### Creating a Theme

```csharp
public class MidnightGoldTheme : ThemeBase
{
    public override string Name => "midnight-gold";
    public override string DisplayName => "Midnight Gold";

    public override Palette LightPalette => new()
    {
        Primary = "#1A237E",
        Secondary = "#FFD700",
        // ...
    };

    public override Palette DarkPalette => new()
    {
        Primary = "#3949AB",
        Secondary = "#FFD700",
        // ...
    };
}
```

### Registering Themes

```csharp
public static class ServiceCollectionExtensions
{
    public static ThemingOptions AddPremiumThemes(this ThemingOptions options)
    {
        options.AddTheme<MidnightGoldTheme>();
        options.AddTheme<RoseGoldTheme>();
        options.AddTheme<AmethystTheme>();
        return options;
    }
}
```

### Themes Included

- `MidnightGoldTheme` - Deep blue with gold accents
- `RoseGoldTheme` - Warm rose with gold accents
- `AmethystTheme` - Purple/violet palette

## Using These as Templates

### Creating Your Own Plugin

1. Copy `SamplePlugin` folder
2. Rename to `YourCompany.YourProduct.YourPlugin`
3. Update namespace in all files
4. Update `.csproj` file name and references
5. Replace pages with your actual features
6. Add to `Plugins.slnx`
7. Reference from Host or load dynamically

### Creating Your Own Theme Pack

1. Copy `PremiumTheme` folder
2. Rename appropriately
3. Replace theme classes with your brand colors
4. Update the extension method
5. Add to `Plugins.slnx`
6. Call your extension method in Host's `Program.cs`

## Removing Examples

If you don't need these anymore:

```bash
# Remove from solution
dotnet sln Plugins.slnx remove src/examples/LowlandTech.Foundry.SamplePlugin
dotnet sln Plugins.slnx remove src/examples/LowlandTech.Foundry.PremiumTheme

# Delete the folder
rm -rf src/examples

# Remove references from Host's .csproj
# Remove theme registration from Host's Program.cs
```

Or just delete the entire `src/examples/` folder and update the solution file.
