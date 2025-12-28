using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public abstract class ThemeBase : ITheme
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract PaletteLight LightPalette { get; }
    public abstract PaletteDark DarkPalette { get; }
    public virtual Typography? Typography => null;
    public virtual LayoutProperties? LayoutProperties => null;
}
