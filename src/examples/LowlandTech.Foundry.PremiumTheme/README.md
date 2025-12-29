# LowlandTech.Foundry.PremiumTheme

A demonstration theme plugin - shows how to implement `IPlugin` with `ThemeFeature` for custom themes.

## What This Project Does

PremiumTheme demonstrates the theming system:

- **IPlugin Implementation**: `PremiumThemePlugin.cs` extends `PluginBase`
- **ThemeFeature**: Each theme is a separate feature that can be enabled/disabled
- **Custom Palettes**: Amethyst, Midnight Gold, Rose Gold themes
- **Runtime Discovery**: Themes discovered and activated via `IPluginManager`

## Plugin Implementation

```csharp
public class PremiumThemePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.premiumtheme",
        Name: "Premium Theme Pack",
        Description: "Additional premium themes for Foundry",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["themes", "premium", "ui"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(this, "amethyst", "Amethyst Theme",
            "Purple-based elegant theme", new AmethystTheme());

        yield return new ThemeFeature(this, "midnight-gold", "Midnight Gold Theme",
            "Luxurious dark theme with gold accents", new MidnightGoldTheme());

        yield return new ThemeFeature(this, "rose-gold", "Rose Gold Theme",
            "Warm pink and rose theme", new RoseGoldTheme());
    }
}
```

## Why It's Standalone

**Like SamplePlugin, this is intentionally separate:**

1. **Plugin Demonstration**: Shows themes as `IPlugin` implementations with `ThemeFeature`.

2. **Feature Granularity**: Each theme is a separate feature - users can enable Amethyst but disable Rose Gold.

3. **Optional Content**: Themes are personal preference. Users should be able to add/remove theme packs without touching the core app.

4. **Plugin System Proof**: Proves that the theming system works with the new `IPlugin` architecture.

## Theme Features

Each theme is wrapped in a `ThemeFeature`:

```csharp
yield return new ThemeFeature(
    plugin: this,
    id: "amethyst",
    name: "Amethyst Theme",
    description: "Purple-based elegant theme",
    theme: new AmethystTheme()
);
```

This allows:
- Individual theme enable/disable via `IPluginManager`
- State persistence (remembers which themes are active)
- Query active themes: `plugins.GetActiveThemes()`

## Template Customization

When using this repo as a template:

**Creating your own theme plugin:**
1. Create a new RCL project
2. Add a reference to PluginCore
3. Create theme classes extending `ThemeBase`
4. Create a plugin class extending `PluginBase`
5. Yield `ThemeFeature` for each theme in `CreateFeatures()`

**Using built-in themes only:**
- Delete PremiumTheme project
- PluginCore includes DefaultTheme, OceanTheme, ForestTheme
- The theme system works without any additional theme plugins

**Offering theme packs:**
- Keep this pattern for premium/addon themes
- Each theme as a separate `ThemeFeature` for granular control
- Themes appear in the theme picker when their feature is enabled

## Key Files

| File | Purpose |
|------|---------|
| `PremiumThemePlugin.cs` | `IPlugin` implementation with `ThemeFeature` for each theme |
| `Theming/AmethystTheme.cs` | Purple/violet color palette |
| `Theming/MidnightGoldTheme.cs` | Dark with gold accents |
| `Theming/RoseGoldTheme.cs` | Pink/rose with copper highlights |

## Theme Structure

```csharp
public class AmethystTheme : ThemeBase
{
    public override string Name => "amethyst";
    public override string DisplayName => "Amethyst (Premium)";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#7c3aed",
        Secondary = "#a855f7",
        // ...
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#a78bfa",
        Secondary = "#c084fc",
        // ...
    };
}
```

## Dependencies

- `PluginCore` - `IPlugin`, `PluginBase`, `ThemeFeature`, `ThemeBase`
- `MudBlazor` - Palette types

## Should You Merge It?

**No, but consider deleting it.**

Like SamplePlugin, this is example content. In a real app, you either:

1. **Delete it**: Use the built-in themes from PluginCore, or create your own theme project

2. **Repurpose it**: Rename it to `YourCompany.YourApp.Themes` and add your brand themes

3. **Keep the pattern**: If you plan to offer theme packs, this shows how to structure them with `IPlugin` and `ThemeFeature`

**What NOT to do**: Don't merge themes into PluginCore or Host. Themes should be distributable units that users can install optionally.
