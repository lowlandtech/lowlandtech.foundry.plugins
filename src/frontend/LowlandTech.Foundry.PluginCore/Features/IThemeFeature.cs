using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Theming;

namespace LowlandTech.Foundry.PluginCore.Features;

/// <summary>
/// A plugin feature that provides one or more themes.
/// </summary>
public interface IThemeFeature : IPluginFeature
{
    /// <summary>
    /// Gets the themes provided by this feature.
    /// </summary>
    IReadOnlyList<ITheme> Themes { get; }
}
