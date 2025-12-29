using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public interface ITheme
{
    string Name { get; }
    string DisplayName { get; }
    PaletteLight LightPalette { get; }
    PaletteDark DarkPalette { get; }
    Typography? Typography { get; }
    LayoutProperties? LayoutProperties { get; }
}
