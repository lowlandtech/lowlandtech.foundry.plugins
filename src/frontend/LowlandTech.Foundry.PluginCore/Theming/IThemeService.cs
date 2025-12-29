using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public interface IThemeService
{
    event Action? OnThemeChanged;

    bool IsDarkMode { get; }
    bool IsInitialized { get; }
    ITheme CurrentTheme { get; }
    MudTheme MudTheme { get; }
    IReadOnlyList<ITheme> AvailableThemes { get; }

    Task InitializeAsync();
    void ToggleDarkMode();
    void SetDarkMode(bool isDark);
    void SetTheme(string themeName);
    void SetTheme(ITheme theme);
    void RegisterTheme(ITheme theme);
}

/// <summary>
/// Interface for persisting theme preferences.
/// Implement this in the Host project with browser storage.
/// </summary>
public interface IThemeStorageProvider
{
    Task<ThemePreferences?> LoadPreferencesAsync();
    Task SavePreferencesAsync(ThemePreferences preferences);
}

/// <summary>
/// Theme preferences to persist.
/// </summary>
public record ThemePreferences(string ThemeName, bool IsDarkMode);
