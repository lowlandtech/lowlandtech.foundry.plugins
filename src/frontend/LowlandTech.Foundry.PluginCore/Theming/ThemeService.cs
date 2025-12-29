using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public class ThemeService : IThemeService
{
    private readonly List<ITheme> _themes = [];
    private readonly IThemeStorageProvider? _storageProvider;
    private readonly bool _defaultToDark;
    private ITheme _currentTheme = null!;
    private bool _isDarkMode;
    private bool _isInitialized;
    private MudTheme _mudTheme = null!;

    public event Action? OnThemeChanged;

    public bool IsDarkMode => _isDarkMode;
    public bool IsInitialized => _isInitialized;
    public ITheme CurrentTheme => _currentTheme;
    public MudTheme MudTheme => _mudTheme;
    public IReadOnlyList<ITheme> AvailableThemes => _themes.AsReadOnly();

    public ThemeService(IEnumerable<ITheme> themes, IThemeStorageProvider? storageProvider = null, bool defaultToDark = false)
    {
        _storageProvider = storageProvider;
        _defaultToDark = defaultToDark;
        _isDarkMode = defaultToDark;

        foreach (var theme in themes)
        {
            _themes.Add(theme);
        }

        if (_themes.Count == 0)
        {
            throw new InvalidOperationException("At least one theme must be registered.");
        }

        _currentTheme = _themes[0];
        BuildMudTheme();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        if (_storageProvider != null)
        {
            try
            {
                var preferences = await _storageProvider.LoadPreferencesAsync();
                if (preferences != null)
                {
                    // Apply saved theme
                    var savedTheme = _themes.FirstOrDefault(t =>
                        t.Name.Equals(preferences.ThemeName, StringComparison.OrdinalIgnoreCase));

                    if (savedTheme != null)
                    {
                        _currentTheme = savedTheme;
                        BuildMudTheme();
                    }

                    _isDarkMode = preferences.IsDarkMode;
                }
            }
            catch
            {
                // Ignore storage errors, use defaults
            }
        }

        _isInitialized = true;
        NotifyThemeChanged();
    }

    public void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
        NotifyThemeChanged();
        SavePreferencesAsync();
    }

    public void SetDarkMode(bool isDark)
    {
        if (_isDarkMode != isDark)
        {
            _isDarkMode = isDark;
            NotifyThemeChanged();
            SavePreferencesAsync();
        }
    }

    public void SetTheme(string themeName)
    {
        var theme = _themes.FirstOrDefault(t =>
            t.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase));

        if (theme != null)
        {
            SetTheme(theme);
        }
    }

    public void SetTheme(ITheme theme)
    {
        if (_currentTheme != theme)
        {
            _currentTheme = theme;
            BuildMudTheme();
            NotifyThemeChanged();
            SavePreferencesAsync();
        }
    }

    public void RegisterTheme(ITheme theme)
    {
        if (!_themes.Any(t => t.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _themes.Add(theme);
        }
    }

    private void BuildMudTheme()
    {
        _mudTheme = new MudTheme
        {
            PaletteLight = _currentTheme.LightPalette,
            PaletteDark = _currentTheme.DarkPalette,
            Typography = _currentTheme.Typography ?? new Typography(),
            LayoutProperties = _currentTheme.LayoutProperties ?? new LayoutProperties()
        };
    }

    private void NotifyThemeChanged()
    {
        OnThemeChanged?.Invoke();
    }

    private async void SavePreferencesAsync()
    {
        if (_storageProvider == null)
            return;

        try
        {
            await _storageProvider.SavePreferencesAsync(
                new ThemePreferences(_currentTheme.Name, _isDarkMode));
        }
        catch
        {
            // Ignore storage errors
        }
    }
}
