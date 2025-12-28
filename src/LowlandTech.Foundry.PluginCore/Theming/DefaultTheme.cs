using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public class DefaultTheme : ThemeBase
{
    public override string Name => "default";
    public override string DisplayName => "Default";

    public override PaletteLight LightPalette => new()
    {
        Primary = Colors.Blue.Darken1,
        Secondary = Colors.DeepPurple.Accent2,
        Tertiary = Colors.Teal.Default,
        AppbarBackground = Colors.Blue.Darken2,
        Background = Colors.Gray.Lighten5,
        Surface = Colors.Shades.White,
        DrawerBackground = Colors.Shades.White,
        DrawerText = Colors.Gray.Darken3,
        Success = Colors.Green.Default,
        Warning = Colors.Orange.Default,
        Error = Colors.Red.Default,
        Info = Colors.Blue.Default
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = Colors.Blue.Lighten1,
        Secondary = Colors.DeepPurple.Accent2,
        Tertiary = Colors.Teal.Lighten1,
        AppbarBackground = Colors.Blue.Darken4,
        Background = Colors.Gray.Darken4,
        Surface = Colors.Gray.Darken3,
        DrawerBackground = Colors.Gray.Darken4,
        DrawerText = Colors.Gray.Lighten3,
        Success = Colors.Green.Lighten1,
        Warning = Colors.Orange.Lighten1,
        Error = Colors.Red.Lighten1,
        Info = Colors.Blue.Lighten1
    };
}
