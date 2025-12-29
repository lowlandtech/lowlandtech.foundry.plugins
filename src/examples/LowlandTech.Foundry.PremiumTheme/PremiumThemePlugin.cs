using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Features;
using LowlandTech.Foundry.PremiumTheme.Theming;

namespace LowlandTech.Foundry.PremiumTheme;

/// <summary>
/// Premium theme pack plugin providing additional high-quality themes.
/// </summary>
public class PremiumThemePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.premiumtheme",
        Name: "Premium Theme Pack",
        Description: "Additional premium themes for Foundry including Amethyst, Midnight Gold, and Rose Gold",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["themes", "premium", "ui"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        yield return new ThemeFeature(
            this,
            id: "amethyst",
            name: "Amethyst Theme",
            description: "Purple-based elegant theme with violet accents",
            theme: new AmethystTheme());

        yield return new ThemeFeature(
            this,
            id: "midnight-gold",
            name: "Midnight Gold Theme",
            description: "Luxurious dark theme with gold accents",
            theme: new MidnightGoldTheme());

        yield return new ThemeFeature(
            this,
            id: "rose-gold",
            name: "Rose Gold Theme",
            description: "Warm pink and rose theme with copper highlights",
            theme: new RoseGoldTheme());
    }
}
