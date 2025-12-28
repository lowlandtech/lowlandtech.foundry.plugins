using LowlandTech.Foundry.PluginCore.Theming;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LowlandTech.Foundry.Host.Services;

public class ThemeStorageProvider : IThemeStorageProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ILogger<ThemeStorageProvider> _logger;

    private const string ThemePreferencesKey = "foundry_theme_preferences";

    public ThemeStorageProvider(ProtectedLocalStorage localStorage, ILogger<ThemeStorageProvider> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<ThemePreferences?> LoadPreferencesAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<ThemePreferences>(ThemePreferencesKey);
            if (result.Success && result.Value != null)
            {
                _logger.LogDebug("Loaded theme preferences: {Theme}, DarkMode: {DarkMode}",
                    result.Value.ThemeName, result.Value.IsDarkMode);
                return result.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load theme preferences from storage");
        }

        return null;
    }

    public async Task SavePreferencesAsync(ThemePreferences preferences)
    {
        try
        {
            await _localStorage.SetAsync(ThemePreferencesKey, preferences);
            _logger.LogDebug("Saved theme preferences: {Theme}, DarkMode: {DarkMode}",
                preferences.ThemeName, preferences.IsDarkMode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save theme preferences to storage");
        }
    }
}
