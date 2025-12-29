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

# Start everything via Aspire
dotnet run --project src/LowlandTech.Foundry.AppHost
```

Aspire will spin up PostgreSQL in Docker, start the API, and launch the Host. Open the Aspire dashboard URL it prints to see all the services.

## How the Plugin System Works

Plugins are just Razor Class Libraries (RCLs) with some attributes on their pages.

### Creating a Plugin

1. Create a new RCL project targeting `net10.0`
2. Reference `LowlandTech.Foundry.PluginCore`
3. Add `[MenuItem]` attributes to pages you want in the nav

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

That's it. The plugin system scans for these attributes and builds the navigation automatically.

### Loading Plugins

Plugins can be loaded three ways:

**1. Direct Assembly Reference** (simplest, for dev)
```csharp
// In Host's Program.cs
builder.Services.AddPluginSystem(
    builder.Configuration,
    typeof(MyPlugin._Imports).Assembly
);
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

### Menu Hierarchy

Pages can be nested under parent menus:

```csharp
// Creates "Reports" parent with "Sales" and "Inventory" children
@attribute [MenuItem(Title = "Sales", ParentMenu = "Reports", Order = 1)]
@attribute [MenuItem(Title = "Inventory", ParentMenu = "Reports", Order = 2)]
```

## Theming

The theme system lets plugins provide custom themes. Themes are just classes implementing `ITheme`:

```csharp
public class MyTheme : ThemeBase
{
    public override string Name => "my-theme";
    public override string DisplayName => "My Custom Theme";

    public override Palette LightPalette => new()
    {
        Primary = "#1976D2",
        Secondary = "#424242",
        // ... other colors
    };

    public override Palette DarkPalette => new()
    {
        Primary = "#90CAF9",
        // ...
    };
}
```

Register themes in your plugin:

```csharp
public static class MyPluginExtensions
{
    public static ThemingOptions AddMyThemes(this ThemingOptions options)
    {
        options.AddTheme<MyTheme>();
        return options;
    }
}
```

Then in the Host:

```csharp
builder.Services.AddFoundryTheming(options =>
{
    options.AddTheme<DefaultTheme>();
    options.AddMyThemes(); // From plugin
});
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

### User Settings

P2P settings are stored per-user in the database:
- Display name and status
- Discovery preferences (LAN/WAN)
- Trusted and blocked peer lists
- Plugin sharing permissions

Server-wide settings (STUN/TURN servers, signaling URL) come from `appsettings.json`.

## Project Structure

```
src/
├── LowlandTech.Foundry.AppHost/       # Aspire orchestration
├── LowlandTech.Foundry.ServiceDefaults/ # Shared Aspire config
├── LowlandTech.Foundry.Api/           # REST API + Identity
├── LowlandTech.Foundry.Host/          # Blazor Server shell
├── LowlandTech.Foundry.PluginCore/    # Plugin framework
├── LowlandTech.Foundry.SamplePlugin/  # Example plugin
├── LowlandTech.Foundry.PremiumTheme/  # Theme plugin example
├── LowlandTech.Foundry.Collaboration/ # Collaboration models
├── LowlandTech.Foundry.Collaboration.UI/ # Collab UI components
├── LowlandTech.Foundry.P2P.Core/      # P2P abstractions
├── LowlandTech.Foundry.P2P.Crdt/      # CRDT implementations
├── LowlandTech.Foundry.P2P.WebRTC/    # WebRTC transport
└── LowlandTech.Foundry.P2P.Signaling/ # Signaling server
```

## Authentication

The Host and API use a token-based auth flow:

1. User logs in via Host UI
2. Host sends credentials to API
3. API validates and returns JWT tokens
4. Host stores tokens in encrypted browser storage
5. Subsequent API calls include the token
6. Tokens auto-refresh before expiry

The API uses ASP.NET Identity with PostgreSQL. Cookie settings prevent CSRF, and tokens are stored using the Data Protection API.

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
mv LowlandTech.Foundry.Plugins.sln YourCompany.YourProduct.sln

# Rename project folders (in src/)
# LowlandTech.Foundry.Host → YourCompany.YourProduct.Host
# etc.
```

**Update .csproj files:**
- Change `<RootNamespace>` and `<AssemblyName>`
- Update project references to new names

**Update the solution file:**
- Edit `.sln` to reference renamed projects

## 2. Remove What You Don't Need

**Don't need P2P collaboration?**
Remove these projects:
- `LowlandTech.Foundry.P2P.Core`
- `LowlandTech.Foundry.P2P.Crdt`
- `LowlandTech.Foundry.P2P.WebRTC`
- `LowlandTech.Foundry.P2P.Signaling`
- `LowlandTech.Foundry.Collaboration`
- `LowlandTech.Foundry.Collaboration.UI`

Remove from Host:
- P2P settings page and service
- Collaboration assembly reference in `Program.cs`
- P2P config from `appsettings.json`

Remove from API:
- `UserP2PSettings` entity and endpoints
- Related migration

**Don't need theming?**
Keep `DefaultTheme` but remove:
- `LowlandTech.Foundry.PremiumTheme`
- Theme switcher UI component
- Extra theme registrations

**Don't need plugins from folders/NuGet?**
Simplify to assembly-only loading:
```csharp
builder.Services.AddPluginSystem(
    typeof(YourPlugin._Imports).Assembly
);
```

Remove folder/NuGet catalog code from PluginCore if you want to slim it down.

## 3. Customize the Host

**Change the app name:**
- Update `MainLayout.razor` - change "Foundry" text
- Update `App.razor` - change page title
- Update favicon

**Change the layout:**
The default uses MudBlazor with a sidebar drawer and top app bar. Modify `MainLayout.razor` for your preferred layout.

**Add your own pages:**
Create pages in Host or in plugin projects. Use `[MenuItem]` to add them to navigation.

## 4. Customize the API

**Change the database:**
The template uses PostgreSQL via Aspire. To use a different database:

1. Update `AppHost/Program.cs` to use different Aspire component
2. Update `Api/Program.cs` to use different EF provider
3. Regenerate migrations

**Add your own entities:**
1. Add entity class in `Api/Data/`
2. Add `DbSet<>` to `ApplicationDbContext`
3. Create migration: `dotnet ef migrations add YourMigration --project src/YourCompany.YourProduct.Api`
4. Add API endpoints in `Program.cs`

**Extend the user model:**
`ApplicationUser` already extends `IdentityUser`. Add your own properties there, update the DbContext configuration, and create a migration.

## 5. Create Your First Plugin

```bash
# Create new RCL
dotnet new razorclasslib -n YourCompany.YourProduct.MyPlugin -o src/YourCompany.YourProduct.MyPlugin

# Add to solution
dotnet sln add src/YourCompany.YourProduct.MyPlugin

# Reference PluginCore
dotnet add src/YourCompany.YourProduct.MyPlugin reference src/YourCompany.YourProduct.PluginCore
```

Add a page with `[MenuItem]` and reference it in Host's `Program.cs`.

## 6. Configure for Production

**Database:**
Set a real PostgreSQL connection string instead of relying on Aspire's dev container.

**Authentication:**
- Configure proper CORS origins
- Set secure cookie settings
- Consider adding external auth providers (Google, Microsoft, etc.)

**P2P (if using):**
- Deploy the signaling server
- Configure STUN/TURN servers for NAT traversal
- Set up the signaling server URL in config

**Hosting:**
The Host is a standard Blazor Server app. Deploy to any host that supports ASP.NET Core (Azure App Service, AWS, containers, etc.).

## 7. Tips

**Keep PluginCore stable.** Once you have plugins depending on it, breaking changes are painful. Version it carefully.

**Use the plugin system for optional features.** Core functionality can live in Host. Plugins are for things users might enable/disable or that you distribute separately.

**Test plugins in isolation.** Each plugin should be testable without the full Host running.

**Consider plugin versioning.** The NuGet catalog supports version constraints. Plan for how you'll handle plugin updates.

---

## License

MIT - see [LICENSE.txt](LICENSE.txt)

## Contributing

Issues and PRs welcome at https://github.com/lowlandtech/lowlandtech.foundry.plugins
