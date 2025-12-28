using MudBlazor;

namespace LowlandTech.Foundry.PluginCore.Theming;

public class ThemeService : IThemeService
{
    private readonly List<ITheme> _themes = [];
    private ITheme _currentTheme = null!;
    private bool _isDarkMode;
    private MudTheme _mudTheme = null!;

    public event Action? OnThemeChanged;

    public bool IsDarkMode => _isDarkMode;
    public ITheme CurrentTheme => _currentTheme;
    public MudTheme MudTheme => _mudTheme;
    public IReadOnlyList<ITheme> AvailableThemes => _themes.AsReadOnly();

    public ThemeService(IEnumerable<ITheme> themes, bool defaultToDark = false)
    {
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

    public void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
        NotifyThemeChanged();
    }

    public void SetDarkMode(bool isDark)
    {
        if (_isDarkMode != isDark)
        {
            _isDarkMode = isDark;
            NotifyThemeChanged();
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
}
