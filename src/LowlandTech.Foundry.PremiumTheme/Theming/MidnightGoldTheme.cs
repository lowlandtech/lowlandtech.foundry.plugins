using LowlandTech.Foundry.PluginCore.Theming;
using MudBlazor;

namespace LowlandTech.Foundry.PremiumTheme.Theming;

public class MidnightGoldTheme : ThemeBase
{
    public override string Name => "midnight-gold";
    public override string DisplayName => "Midnight Gold (Premium)";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#b8860b",         // Dark goldenrod
        Secondary = "#1e293b",       // Slate
        Tertiary = "#d4a017",        // Gold
        AppbarBackground = "#1e293b",
        Background = "#fffbeb",      // Warm cream
        Surface = Colors.Shades.White,
        DrawerBackground = "#fef3c7",
        DrawerText = "#78350f",
        Success = "#059669",
        Warning = "#d97706",
        Error = "#dc2626",
        Info = "#0284c7"
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#fbbf24",         // Amber
        Secondary = "#d4a017",       // Gold
        Tertiary = "#f59e0b",        // Orange-gold
        AppbarBackground = "#0c0a09",
        Background = "#0c0a09",      // Almost black with warm tint
        Surface = "#1c1917",         // Warm dark gray
        DrawerBackground = "#0c0a09",
        DrawerText = "#fef3c7",
        Success = "#34d399",
        Warning = "#fbbf24",
        Error = "#f87171",
        Info = "#38bdf8"
    };

    public override Typography Typography => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Playfair Display", "Georgia", "serif"]
        }
    };
}
