# Foundry Plugins

A modular Blazor Server application with a plugin architecture, real-time P2P collaboration, and .NET Aspire orchestration. Built as a foundation for extensible web applications where plugins can add pages, themes, and functionality without modifying the core.

## What This Is

This is a starter template for building plugin-based Blazor applications. The core idea is simple: instead of building a monolithic app where adding features means editing core code, you build features as separate plugin assemblies that get discovered and loaded at runtime.

Think of it like VS Code extensions or WordPress plugins, but for Blazor Server apps.

**The main pieces:**

- **Host** - The Blazor Server UI shell. Handles navigation, layout, and renders pages from plugins.
- **API** - REST API with Identity authentication. Stores user data in PostgreSQL.
- **PluginCore** - The framework that makes plugins work. Handles discovery, loading, menus, and theming.
- **AppHost** - Aspire orchestration that wires everything together for local dev.

## Quick Start

You'll need:
- .NET 10 SDK
- Docker (for PostgreSQL via Aspire)

```bash
# Clone and run
git clone https://github.com/lowlandtech/lowlandtech.foundry.plugins.git
cd lowlandtech.foundry.plugins

# Build the solution
dotnet build Plugins.slnx

# Start everything via Aspire
dotnet run --project src/backend/LowlandTech.Foundry.AppHost
```

Aspire will spin up PostgreSQL in Docker, start the API, and launch the Host. Open the Aspire dashboard URL it prints to see all the services.

## How the Plugin System Works

The plugin system has two layers:

1. **IPlugin Interface** - Full lifecycle management with features that can be enabled/disabled
2. **Convention-based Discovery** - Attribute-based menu registration via `[MenuItem]`

### The IPlugin Architecture

Plugins implement `IPlugin` (or extend `PluginBase`) and expose capabilities via `IPluginFeature`:

```csharp
public class MyPlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "mycompany.myplugin",
        Name: "My Plugin",
        Description: "Does cool stuff",
        Version: new Version(1, 0, 0),
        Author: "My Company",
        Tags: ["feature", "demo"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(this, "dark-mode", "Dark Mode",
            "Custom dark theme", new MyDarkTheme());

        yield return new MenuFeature(this, "navigation", "Navigation",
            "Plugin menu items", GetMenuItems());
    }
}
```

**Plugin Lifecycle:**

```
Discovered → Installed → Activated ↔ Disabled
                ↓
              Error
```

- State persists in the database across restarts
- Features can be individually enabled/disabled
- Events fire on state changes (`PluginStateChanged`, `FeatureStateChanged`)

**Inject plugins anywhere:**
```csharp
// Get all plugins
public MyService(IEnumerable<IPlugin> plugins) { }

// Get the plugin manager for lifecycle control
public MyService(IPluginManager pluginManager)
{
    await pluginManager.ActivatePluginAsync("mycompany.myplugin");
    await pluginManager.EnableFeatureAsync("mycompany.myplugin", "dark-mode");
}
```

### Creating a Plugin

1. Create a new RCL project targeting `net10.0`
2. Reference `LowlandTech.Foundry.PluginCore`
3. Implement `IPlugin` (or extend `PluginBase`)
4. Create features using `ThemeFeature`, `MenuFeature`, or custom implementations

```csharp
// MyPlugin.cs
public class MyPlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "mycompany.myplugin",
        Name: "My Plugin",
        Description: "A sample plugin",
        Version: new Version(1, 0, 0),
        Author: "My Company",
        Tags: ["sample"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        // Theme feature
        yield return new ThemeFeature(this, "custom-theme",
            "Custom Theme", "A beautiful theme", new MyTheme());

        // Menu feature with items
        yield return new MenuFeature(this, "navigation",
            "Navigation", "Plugin pages", new[]
            {
                new MenuItemInfo { Title = "Dashboard", Route = "/dashboard", Icon = "..." }
            });
    }
}
```

### Loading Plugins

**1. Using AddPlugins()** (recommended)
```csharp
// In Host's Program.cs
builder.Services.AddPlugins(options =>
{
    options.AddAssemblyOf<MyPlugin>();
    options.AddAssemblyOf<AnotherPlugin>();
});
```

**2. From Folders** (drop DLLs in a folder)
```json
// appsettings.json
{
  "Plugins": {
    "Folders": [
      {
        "Path": "./plugins",
        "SearchPatterns": ["*.Plugin.dll"]
      }
    ]
  }
}
```

**3. From NuGet** (for distribution)
```json
{
  "Plugins": {
    "NuGetPackages": [
      { "PackageId": "MyCompany.CoolPlugin", "Version": "1.0.0" }
    ]
  }
}
```

### Plugin Features

Features are individual capabilities within a plugin that can be enabled/disabled:

| Feature Type | Purpose |
|-------------|---------|
| `ThemeFeature` | Provides themes for the theme switcher |
| `MenuFeature` | Provides navigation menu items |
| Custom | Implement `IPluginFeature` for your own |

```csharp
// Enable/disable features at runtime
await pluginManager.EnableFeatureAsync("mycompany.myplugin", "custom-theme");
await pluginManager.DisableFeatureAsync("mycompany.myplugin", "navigation");
```

### Menu Items via Attributes

Pages can use `[MenuItem]` attributes for automatic navigation discovery:

```csharp
@page "/my-feature"
@attribute [MenuItem(
    Title = "My Feature",
    Icon = Icons.Material.Filled.Star,
    Location = MenuLocation.Sidebar,
    Order = 100
)]

<h1>Hello from my plugin!</h1>
```

Nested menus:
```csharp
// Creates "Reports" parent with "Sales" and "Inventory" children
@attribute [MenuItem(Title = "Sales", ParentMenu = "Reports", Order = 1)]
@attribute [MenuItem(Title = "Inventory", ParentMenu = "Reports", Order = 2)]
```

## Theming

Themes can be provided via `ThemeFeature` in plugins:

```csharp
public class MyThemePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "mycompany.themes",
        Name: "Theme Pack",
        Description: "Custom themes",
        Version: new Version(1, 0, 0),
        Author: "My Company",
        Tags: ["themes"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(this, "ocean", "Ocean Theme",
            "Cool blue ocean theme", new OceanTheme());
        yield return new ThemeFeature(this, "forest", "Forest Theme",
            "Natural green theme", new ForestTheme());
    }
}
```

Theme classes implement `ITheme` (or extend `ThemeBase`):

```csharp
public class MyTheme : ThemeBase
{
    public override string Name => "my-theme";
    public override string DisplayName => "My Custom Theme";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#1976D2",
        Secondary = "#424242",
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#90CAF9",
    };
}
```

Users can switch themes from the UI. Preferences persist in browser storage.

## P2P Collaboration

The P2P system enables real-time collaboration without a central server. It uses:

- **WebRTC** for peer connections (via SIPSorcery)
- **CRDTs** for conflict-free data sync
- **mDNS** for LAN peer discovery
- **SignalR** signaling server for WAN discovery

### How It Works

Each user gets a cryptographic identity (Ed25519 keypair). When peers connect, they authenticate each other and establish encrypted channels. Data syncs using CRDTs, so there's no conflict resolution needed - edits from all peers merge automatically.

The collaboration features include:
- Real-time chat rooms
- Collaborative document editing
- Presence (who's online, cursor positions)
- Plugin sharing between trusted peers

## Project Structure

The solution is organized into three folders for clear separation:

```
src/
├── frontend/                              # UI and client-side libraries
│   ├── LowlandTech.Foundry.PluginCore/    # Plugin framework (shared contract)
│   ├── LowlandTech.Foundry.Host/          # Blazor Server shell
│   ├── LowlandTech.Foundry.P2P/           # P2P networking + CRDTs + WebRTC
│   └── LowlandTech.Foundry.Collaboration/ # Collaboration features + UI
│
├── backend/                               # Server-side services
│   ├── LowlandTech.Foundry.Api/           # REST API + Identity
│   ├── LowlandTech.Foundry.AppHost/       # Aspire orchestration
│   ├── LowlandTech.Foundry.ServiceDefaults/ # Shared Aspire config
│   └── LowlandTech.Foundry.P2P.Signaling/ # WebRTC signaling server
│
└── examples/                              # Example plugins (delete for production)
    ├── LowlandTech.Foundry.SamplePlugin/  # Example plugin with pages
    └── LowlandTech.Foundry.PremiumTheme/  # Example theme plugin
```

**10 projects total** (down from 13):
- P2P.Core + P2P.Crdt + P2P.WebRTC merged into **P2P**
- Collaboration + Collaboration.UI merged into **Collaboration**

## Authentication

The Host and API use a token-based auth flow:

1. User logs in via Host UI
2. Host sends credentials to API
3. API validates and returns JWT tokens
4. Host stores tokens in encrypted browser storage
5. Subsequent API calls include the token
6. Tokens auto-refresh before expiry

The API uses ASP.NET Identity with PostgreSQL.

---

# Using This as a Template

If you want to use this as a starting point for your own project, here's how to customize it.

## 1. Rename the Solution

The namespace is `LowlandTech.Foundry`. You'll want to change this to your own.

**Find and replace across all files:**
- `LowlandTech.Foundry` → `YourCompany.YourProduct`
- `lowlandtech.foundry` → `yourcompany.yourproduct`
- `Foundry` → `YourProduct` (in display strings)

**Rename the physical files:**
```bash
# Rename solution file
mv Plugins.slnx YourProduct.slnx

# Rename project folders (in src/frontend/, src/backend/, src/examples/)
```

**Update .csproj files:**
- Change `<RootNamespace>` and `<AssemblyName>`
- Update project references to new names

## 2. Remove What You Don't Need

**Don't need P2P collaboration?**
Remove these projects:
- `src/frontend/LowlandTech.Foundry.P2P`
- `src/frontend/LowlandTech.Foundry.Collaboration`
- `src/backend/LowlandTech.Foundry.P2P.Signaling`

Remove from Host:
- P2P settings page and service
- Collaboration assembly reference in `Program.cs`
- P2P config from `appsettings.json`

Remove from API:
- `UserP2PSettings` entity and endpoints

**Don't need theming?**
Keep `DefaultTheme` but remove:
- `src/examples/LowlandTech.Foundry.PremiumTheme`
- Theme switcher UI component

**Don't need example plugins?**
Delete the entire `src/examples/` folder.

## 3. Customize the Host

**Change the app name:**
- Update `MainLayout.razor` - change "Foundry" text
- Update `App.razor` - change page title
- Update favicon

**Change the layout:**
The default uses MudBlazor with a sidebar drawer and top app bar. Modify `MainLayout.razor` for your preferred layout.

## 4. Customize the API

**Change the database:**
The template uses PostgreSQL via Aspire. To use a different database:

1. Update `AppHost/Program.cs` to use different Aspire component
2. Update `Api/Program.cs` to use different EF provider
3. Regenerate migrations

**Add your own entities:**
1. Add entity class in `Api/Data/`
2. Add `DbSet<>` to `ApplicationDbContext`
3. Create migration: `dotnet ef migrations add YourMigration --project src/backend/YourCompany.YourProduct.Api`
4. Add API endpoints in `Program.cs`

## 5. Create Your First Plugin

```bash
# Create new RCL
dotnet new razorclasslib -n YourCompany.YourProduct.MyPlugin -o src/examples/YourCompany.YourProduct.MyPlugin

# Add to solution
dotnet sln Plugins.slnx add src/examples/YourCompany.YourProduct.MyPlugin

# Reference PluginCore
dotnet add src/examples/YourCompany.YourProduct.MyPlugin reference src/frontend/YourCompany.YourProduct.PluginCore
```

Create a plugin class:
```csharp
// MyPlugin.cs
public class MyPlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "yourcompany.myplugin",
        Name: "My Plugin",
        Description: "My first plugin",
        Version: new Version(1, 0, 0),
        Author: "Your Company",
        Tags: ["example"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new MenuFeature(this, "nav", "Navigation",
            "Menu items", DiscoverMenuItems());
    }
}
```

Register in Host's `Program.cs`:
```csharp
builder.Services.AddPlugins(options =>
{
    options.AddAssemblyOf<MyPlugin>();
});
```

## 6. Tips

**Keep PluginCore stable.** Once you have plugins depending on it, breaking changes are painful. Version it carefully.

**Use the plugin system for optional features.** Core functionality can live in Host. Plugins are for things users might enable/disable or that you distribute separately.

**Test plugins in isolation.** Each plugin should be testable without the full Host running.

---

## License

MIT - see [LICENSE.txt](LICENSE.txt)

## Contributing

Issues and PRs welcome at https://github.com/lowlandtech/lowlandtech.foundry.plugins
