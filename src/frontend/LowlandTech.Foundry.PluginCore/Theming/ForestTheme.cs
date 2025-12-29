using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public class ForestTheme : ThemeBase
{
    public override string Name => "forest";
    public override string DisplayName => "Forest";

    public override PaletteLight LightPalette => new()
    {
        Primary = Colors.Green.Darken2,
        Secondary = Colors.Brown.Default,
        Tertiary = Colors.Lime.Darken1,
        AppbarBackground = Colors.Green.Darken3,
        Background = "#f5f5f0",
        Surface = Colors.Shades.White,
        DrawerBackground = Colors.Shades.White,
        DrawerText = Colors.Gray.Darken3,
        Success = Colors.LightGreen.Default,
        Warning = Colors.Orange.Default,
        Error = Colors.DeepOrange.Default,
        Info = Colors.Teal.Default
    };

    public override PaletteDark DarkPalette => new()
    {
        Primary = Colors.Green.Lighten1,
        Secondary = Colors.Brown.Lighten2,
        Tertiary = Colors.Lime.Lighten1,
        AppbarBackground = "#1b3d1b",
        Background = "#1a2e1a",
        Surface = "#243524",
        DrawerBackground = "#1a2e1a",
        DrawerText = Colors.Gray.Lighten3,
        Success = Colors.LightGreen.Lighten1,
        Warning = Colors.Orange.Lighten1,
        Error = Colors.DeepOrange.Lighten1,
        Info = Colors.Teal.Lighten1
    };
}
