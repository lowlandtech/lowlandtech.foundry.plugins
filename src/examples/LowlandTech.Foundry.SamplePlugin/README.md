# LowlandTech.Foundry.SamplePlugin

A demonstration plugin - shows how to create plugins with menu items, submenus, and custom page layouts.

## What This Project Does

SamplePlugin demonstrates the plugin system:

- **Menu Registration**: Uses `[MenuItem]` to add navigation items
- **Submenu Support**: Shows nested menu structures (Reports → Sales, Inventory)
- **Custom Layouts**: Uses `[PageLayout]` for the FullScreen page
- **Basic Pages**: Dashboard, Reports, Settings examples

## Why It's Standalone

**This is intentionally a separate assembly.** Here's why:

1. **Plugin Demonstration**: The whole point is to show plugins as separate DLLs. If it were merged, it wouldn't demonstrate the plugin architecture.

2. **Template Example**: When someone uses this repo as a template, they can look at SamplePlugin to understand how to create their own plugins.

3. **Optional Content**: In a real app, you'd remove this and replace with your actual plugins. Having it separate makes removal trivial.

4. **Dynamic Loading Test**: The Host loads this via `AssemblyPluginCatalog` or `FolderPluginCatalog`. It proves the loading mechanisms work.

## Template Customization

When using this repo as a template:

**Learning from it:**
- Study how `[MenuItem]` attributes configure navigation
- See how pages register in different menu locations (Sidebar, Topbar)
- Understand submenu patterns

**Creating your own plugins:**
1. Copy SamplePlugin as a starting point
2. Rename the project and namespace
3. Modify the pages for your features
4. Update `[MenuItem]` attributes for your navigation

**Removing it:**
- Delete the project
- Remove the reference from Host
- The plugin system still works - just no sample content

## Key Files

| File | Purpose |
|------|---------|
| `Pages/Dashboard.razor` | Basic sidebar page example |
| `Pages/Reports.razor` | Page with submenu children |
| `Pages/SalesReport.razor` | Submenu child page |
| `Pages/InventoryReport.razor` | Submenu child page |
| `Pages/Settings.razor` | Another sidebar page |
| `Pages/FullScreen.razor` | Custom layout example |

## Dependencies

- `MudBlazor` - UI framework
- `PluginCore` - Plugin framework (for attributes)

## Should You Merge It?

**Definitely not.**

The entire purpose of this project is to be a separate assembly that demonstrates the plugin system. Merging it would defeat its purpose.

However, you should **delete it** when building a real application. It's example content, not production code. Replace it with your actual plugins.

**What to do:**
1. Keep it while learning the plugin system
2. Use it as a reference when building your plugins
3. Delete it before shipping your app
4. Or keep it and rename it to your first actual plugin
