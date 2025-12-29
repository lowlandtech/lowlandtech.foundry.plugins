using LowlandTech.Foundry.PluginCore.Extensions;
using LowlandTech.Foundry.PremiumTheme.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PremiumTheme.Extensions;

public static class ServiceCollectionExtensions
{
    public static ThemingOptions AddPremiumThemes(this ThemingOptions options)
    {
        options.AddTheme<AmethystTheme>();
        options.AddTheme<MidnightGoldTheme>();
        options.AddTheme<RoseGoldTheme>();
        return options;
    }
}
