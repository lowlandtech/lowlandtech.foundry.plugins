using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public class OceanTheme : ThemeBase
{
    public override string Name => "ocean";
    public override string DisplayName => "Ocean";

    public override PaletteLight LightPalette => new()
    {
        Primary = Colors.Cyan.Darken1,
        Secondary = Colors.Teal.Accent3,
        Tertiary = Colors.LightBlue.Default,
        AppbarBackground = Colors.Cyan.Darken2,
        Background = Colors.Gray.Lighten5,
        Surface = Colors.Shades.White,
        DrawerBackground = Colors.Shades.White,
        DrawerText = Colors.Gray.Darken3,
        Success = Colors.Green.Default,
        Warning = Colors.Amber.Default,
        Error = Colors.Red.Default,
        Info = Colors.LightBlue.Default
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = Colors.Cyan.Lighten1,
        Secondary = Colors.Teal.Accent2,
        Tertiary = Colors.LightBlue.Lighten1,
        AppbarBackground = Colors.Cyan.Darken4,
        Background = "#0a1929",
        Surface = "#0d2137",
        DrawerBackground = "#0a1929",
        DrawerText = Colors.Gray.Lighten3,
        Success = Colors.Green.Lighten1,
        Warning = Colors.Amber.Lighten1,
        Error = Colors.Red.Lighten1,
        Info = Colors.LightBlue.Lighten1
    };
}
