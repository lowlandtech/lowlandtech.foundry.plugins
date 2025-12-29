using LowlandTech.Foundry.PluginCore.Theming;
using MudBlazor;

namespace LowlandTech.Foundry.PremiumTheme.Theming;

public class AmethystTheme : ThemeBase
{
    public override string Name => "amethyst";
    public override string DisplayName => "Amethyst (Premium)";

    public override PaletteLight LightPalette => new()
    {
        Primary = "#7c3aed",         // Violet
        Secondary = "#a855f7",       // Purple
        Tertiary = "#c084fc",        // Light purple
        AppbarBackground = "#5b21b6",
        Background = "#faf5ff",      // Very light purple tint
        Surface = Colors.Shades.White,
        DrawerBackground = "#f5f3ff",
        DrawerText = "#4c1d95",
        Success = "#10b981",
        Warning = "#f59e0b",
        Error = "#ef4444",
        Info = "#8b5cf6"
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = "#a78bfa",         // Light violet
        Secondary = "#c084fc",       // Light purple
        Tertiary = "#e879f9",        // Pink-purple
        AppbarBackground = "#2e1065",
        Background = "#0f0720",      // Very dark purple
        Surface = "#1e1033",
        DrawerBackground = "#170a2e",
        DrawerText = "#e9d5ff",
        Success = "#34d399",
        Warning = "#fbbf24",
        Error = "#f87171",
        Info = "#a78bfa"
    };

    public override Typography Typography => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"]
        }
    };
}
