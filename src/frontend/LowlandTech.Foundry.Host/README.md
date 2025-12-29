# LowlandTech.Foundry.Host

The Blazor Server application - the main UI that users interact with. This project wires everything together: plugins, themes, authentication, and P2P collaboration.

## What This Project Does

Host is the runnable web application that:

- Discovers and activates plugins via `IPluginManager`
- Renders the MudBlazor UI with plugin-provided menus and themes
- Handles user authentication via the API
- Manages plugin lifecycle (install, activate, enable/disable features)
- Provides the P2P settings UI and collaboration features
- Serves as the Blazor Server entry point

## Why It's Standalone

**This project should remain separate from the API.** Here's why:

1. **Separation of Concerns**: The Host is a UI application; the API is a backend service. They have different responsibilities, scaling characteristics, and deployment patterns.

2. **Blazor Server vs API**: The Host uses Blazor Server with SignalR circuits for real-time UI updates. The API is a stateless REST service. These are fundamentally different execution models.

3. **Plugin Host**: This is where plugins actually run. Plugins render their Razor components here, not in the API.

4. **Independent Scaling**: You might want to run multiple Host instances behind a load balancer while having a single API. Or scale the API horizontally while keeping a single Host.

## Plugin Registration

The Host registers plugins using the new `IPlugin` architecture:

```csharp
// Program.cs
builder.Services.AddPlugins(options =>
{
    options.AddAssemblyOf<SamplePlugin>();
    options.AddAssemblyOf<PremiumThemePlugin>();
});
```

This enables:
- Automatic plugin discovery on startup
- `IEnumerable<IPlugin>` injectable anywhere
- `IPluginManager` for lifecycle control
- Plugin/feature state persistence

## Template Customization

When using this repo as a template:

- Modify `Components/Layout/MainLayout.razor` for your app shell
- Update `wwwroot/` for your static assets
- Configure services in `Program.cs` based on which features you need
- Add your own plugins via `options.AddAssemblyOf<YourPlugin>()`
- Remove P2P references if you don't need collaboration features

## Key Directories

| Directory | Purpose |
|-----------|---------|
| `Components/Layout/` | Main application layout and shell |
| `Components/Pages/` | Built-in pages (Home, Settings, P2P) |
| `Services/` | HTTP clients for API communication |
| `wwwroot/` | Static files (CSS, JS, images) |

## Dependencies

This project references almost everything - it's the integration point:

- `PluginCore` - Plugin framework (`IPlugin`, `IPluginManager`, themes, menus)
- `ServiceDefaults` - Aspire integration
- `Collaboration` - P2P UI components
- `P2P` - P2P networking infrastructure
- `SamplePlugin`, `PremiumTheme` - Example plugins (implementing `IPlugin`)

## Should You Merge It?

**No, but you could slim it down.** The Host needs to stay separate from the API, but you might consider:

- Removing P2P references if you don't need collaboration
- Removing example plugins (SamplePlugin, PremiumTheme) and loading them dynamically instead
- The core Host functionality should remain its own project
