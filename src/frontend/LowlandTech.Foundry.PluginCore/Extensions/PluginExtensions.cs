using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Features;
using LowlandTech.Foundry.PluginCore.Models;
using LowlandTech.Foundry.PluginCore.Theming;

namespace LowlandTech.Foundry.PluginCore.Extensions;

/// <summary>
/// Extension methods for querying plugin features.
/// </summary>
public static class PluginExtensions
{
    /// <summary>
    /// Gets all themes from a plugin's enabled theme features.
    /// </summary>
    /// <param name="plugin">The plugin to query.</param>
    /// <returns>All themes from enabled theme features.</returns>
    public static IEnumerable<ITheme> GetThemes(this IPlugin plugin)
        => plugin.Features
            .OfType<IThemeFeature>()
            .Where(f => f.IsEnabled)
            .SelectMany(f => f.Themes);

    /// <summary>
    /// Gets all menu items from a plugin's enabled menu features.
    /// </summary>
    /// <param name="plugin">The plugin to query.</param>
    /// <returns>All menu items from enabled menu features.</returns>
    public static IEnumerable<MenuItemInfo> GetMenuItems(this IPlugin plugin)
        => plugin.Features
            .OfType<IMenuFeature>()
            .Where(f => f.IsEnabled)
            .SelectMany(f => f.MenuItems);

    /// <summary>
    /// Gets all themes from all activated plugins with enabled theme features.
    /// </summary>
    /// <param name="plugins">The plugins to query.</param>
    /// <returns>All themes from activated plugins.</returns>
    public static IEnumerable<ITheme> GetAllThemes(this IEnumerable<IPlugin> plugins)
        => plugins
            .Where(p => p.State == PluginState.Activated)
            .SelectMany(p => p.GetThemes());

    /// <summary>
    /// Gets all menu items from all activated plugins with enabled menu features.
    /// </summary>
    /// <param name="plugins">The plugins to query.</param>
    /// <returns>All menu items from activated plugins.</returns>
    public static IEnumerable<MenuItemInfo> GetAllMenuItems(this IEnumerable<IPlugin> plugins)
        => plugins
            .Where(p => p.State == PluginState.Activated)
            .SelectMany(p => p.GetMenuItems());

    /// <summary>
    /// Gets all features of a specific type from a plugin.
    /// </summary>
    /// <typeparam name="T">The type of feature to find.</typeparam>
    /// <param name="plugin">The plugin to query.</param>
    /// <returns>All features of the specified type.</returns>
    public static IEnumerable<T> GetFeatures<T>(this IPlugin plugin) where T : IPluginFeature
        => plugin.Features.OfType<T>();

    /// <summary>
    /// Gets all enabled features of a specific type from a plugin.
    /// </summary>
    /// <typeparam name="T">The type of feature to find.</typeparam>
    /// <param name="plugin">The plugin to query.</param>
    /// <returns>All enabled features of the specified type.</returns>
    public static IEnumerable<T> GetEnabledFeatures<T>(this IPlugin plugin) where T : IPluginFeature
        => plugin.Features.OfType<T>().Where(f => f.IsEnabled);

    /// <summary>
    /// Checks if a plugin is currently active.
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <returns>True if the plugin is activated, false otherwise.</returns>
    public static bool IsActive(this IPlugin plugin)
        => plugin.State == PluginState.Activated;

    /// <summary>
    /// Checks if a plugin can be activated (is installed but not currently active).
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <returns>True if the plugin can be activated, false otherwise.</returns>
    public static bool CanActivate(this IPlugin plugin)
        => plugin.State is PluginState.Installed or PluginState.Disabled;

    /// <summary>
    /// Checks if a plugin can be installed (has been discovered but not yet installed).
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <returns>True if the plugin can be installed, false otherwise.</returns>
    public static bool CanInstall(this IPlugin plugin)
        => plugin.State == PluginState.Discovered;
}
