using LowlandTech.Foundry.PluginCore.Theming;
using MudBlazor;

namespace LowlandTech.Foundry.PremiumTheme.Theming;

public class RoseGoldTheme : ThemeBase
{
    public override string Name => "rose-gold";
    public override string DisplayName => "Rose Gold (Premium)";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#be185d",         // Pink-rose
        Secondary = "#b45309",       // Copper/bronze
        Tertiary = "#e11d48",        // Rose
        AppbarBackground = "#9d174d",
        Background = "#fff1f2",      // Very light pink
        Surface = Colors.Shades.White,
        DrawerBackground = "#ffe4e6",
        DrawerText = "#881337",
        Success = "#059669",
        Warning = "#d97706",
        Error = "#dc2626",
        Info = "#0891b2"
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#fb7185",         // Light rose
        Secondary = "#fbbf24",       // Gold accent
        Tertiary = "#f472b6",        // Pink
        AppbarBackground = "#1f0a12",
        Background = "#1a0a10",      // Very dark rose
        Surface = "#2d1219",
        DrawerBackground = "#1a0a10",
        DrawerText = "#fecdd3",
        Success = "#34d399",
        Warning = "#fbbf24",
        Error = "#f87171",
        Info = "#22d3ee"
    };

    public override Typography Typography => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Montserrat", "Segoe UI", "Roboto", "sans-serif"]
        }
    };
}
