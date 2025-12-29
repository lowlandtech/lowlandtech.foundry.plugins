using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Features;

/// <summary>
/// A plugin feature that provides one or more themes.
/// </summary>
public class ThemeFeature : PluginFeatureBase, IThemeFeature
{
    private readonly List<ITheme> _themes;

    /// <summary>
    /// Creates a new theme feature with a single theme.
    /// </summary>
    /// <param name="plugin">The plugin that owns this feature.</param>
    /// <param name="id">Unique identifier for this feature.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Description of the feature.</param>
    /// <param name="theme">The theme provided by this feature.</param>
    public ThemeFeature(IPlugin plugin, string id, string name, string description, ITheme theme)
        : base(plugin, id, name, description)
    {
        _themes = [theme ?? throw new ArgumentNullException(nameof(theme))];
    }

    /// <summary>
    /// Creates a new theme feature with multiple themes.
    /// </summary>
    /// <param name="plugin">The plugin that owns this feature.</param>
    /// <param name="id">Unique identifier for this feature.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Description of the feature.</param>
    /// <param name="themes">The themes provided by this feature.</param>
    public ThemeFeature(IPlugin plugin, string id, string name, string description, IEnumerable<ITheme> themes)
        : base(plugin, id, name, description)
    {
        _themes = themes?.ToList() ?? throw new ArgumentNullException(nameof(themes));
        if (_themes.Count == 0)
        {
            throw new ArgumentException("At least one theme must be provided.", nameof(themes));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ITheme> Themes => _themes;

    /// <inheritdoc />
    public override void Register(IServiceCollection services)
    {
        if (!IsEnabled) return;

        foreach (var theme in _themes)
        {
            services.AddSingleton(theme);
        }
    }
}
