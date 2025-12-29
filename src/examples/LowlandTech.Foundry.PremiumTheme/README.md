# LowlandTech.Foundry.PremiumTheme

A demonstration theme plugin - shows how to create themes that integrate with the theming system.

## What This Project Does

PremiumTheme demonstrates the theming system:

- Implements the `ITheme` interface from PluginCore
- Provides custom color palettes (Sunset, Midnight, etc.)
- Shows how themes can be bundled as plugins
- Demonstrates runtime theme discovery and switching

## Why It's Standalone

**Like SamplePlugin, this is intentionally separate:**

1. **Theme Demonstration**: Shows themes as distributable packages that users can install.

2. **Monetization Example**: The "Premium" naming suggests a business model - free themes in the core, paid themes as plugins.

3. **Optional Content**: Themes are personal preference. Users should be able to add/remove theme packs without touching the core app.

4. **Plugin System Proof**: Proves that the theming system works with dynamically loaded assemblies, not just built-in themes.

## Template Customization

When using this repo as a template:

**Creating your own themes:**
1. Create a new RCL project
2. Add a reference to PluginCore
3. Implement `ITheme` or extend `ThemeBase`
4. Register your themes for discovery

**Using built-in themes only:**
- Delete PremiumTheme project
- PluginCore includes DefaultTheme, OceanTheme, ForestTheme
- The theme system works without any additional theme plugins

**Offering theme packs:**
- Keep this pattern for premium/addon themes
- Users install theme plugins like any other plugin
- Themes appear in the theme picker automatically

## Key Files

| File | Purpose |
|------|---------|
| `Themes/SunsetTheme.cs` | Warm orange/red palette |
| `Themes/MidnightTheme.cs` | Dark blue palette |
| (other theme files) | Additional color schemes |

## Dependencies

- `PluginCore` - Theme interfaces

## Should You Merge It?

**No, but consider deleting it.**

Like SamplePlugin, this is example content. In a real app, you either:

1. **Delete it**: Use the built-in themes from PluginCore, or create your own theme project for your app's branding

2. **Repurpose it**: Rename it to something like `LowlandTech.YourApp.Themes` and put your actual brand themes here

3. **Keep the pattern**: If you plan to sell/distribute theme packs, this shows how to structure them

**What NOT to do**: Don't merge themes into PluginCore or Host. Themes should be distributable units that users can install optionally.

---

## Summary: Suggested Project Consolidation

Based on writing these READMEs, here's a realistic simplification for the template:

| Current (13 projects) | Suggested (8 projects) |
|-----------------------|------------------------|
| PluginCore | **PluginCore** (keep) |
| Host | **Host** (keep) |
| Api | **Api** (keep) |
| AppHost | **AppHost** (keep, required by Aspire) |
| ServiceDefaults | **ServiceDefaults** (keep or inline) |
| P2P.Core | **P2P** (merge Core + WebRTC + Crdt) |
| P2P.Crdt | ↑ merged |
| P2P.WebRTC | ↑ merged |
| P2P.Signaling | **P2P.Signaling** (keep, it's a service) |
| Collaboration | **Collaboration** (merge logic + UI) |
| Collaboration.UI | ↑ merged |
| SamplePlugin | Delete or keep as example |
| PremiumTheme | Delete or keep as example |

This reduces from 13 to 8 core projects (or 6 if you remove the examples).
