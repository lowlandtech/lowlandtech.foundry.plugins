using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public interface IThemeService
{
    event Action? OnThemeChanged;

    bool IsDarkMode { get; }
    ITheme CurrentTheme { get; }
    MudTheme MudTheme { get; }
    IReadOnlyList<ITheme> AvailableThemes { get; }

    void ToggleDarkMode();
    void SetDarkMode(bool isDark);
    void SetTheme(string themeName);
    void SetTheme(ITheme theme);
    void RegisterTheme(ITheme theme);
}
