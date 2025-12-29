using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Features;

/// <summary>
/// A plugin feature that provides menu items for navigation.
/// </summary>
public class MenuFeature : PluginFeatureBase, IMenuFeature
{
    private readonly List<MenuItemInfo> _menuItems;

    /// <summary>
    /// Creates a new menu feature with a single menu item.
    /// </summary>
    /// <param name="plugin">The plugin that owns this feature.</param>
    /// <param name="id">Unique identifier for this feature.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Description of the feature.</param>
    /// <param name="menuItem">The menu item provided by this feature.</param>
    public MenuFeature(IPlugin plugin, string id, string name, string description, MenuItemInfo menuItem)
        : base(plugin, id, name, description)
    {
        _menuItems = [menuItem ?? throw new ArgumentNullException(nameof(menuItem))];
    }

    /// <summary>
    /// Creates a new menu feature with multiple menu items.
    /// </summary>
    /// <param name="plugin">The plugin that owns this feature.</param>
    /// <param name="id">Unique identifier for this feature.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Description of the feature.</param>
    /// <param name="menuItems">The menu items provided by this feature.</param>
    public MenuFeature(IPlugin plugin, string id, string name, string description, IEnumerable<MenuItemInfo> menuItems)
        : base(plugin, id, name, description)
    {
        _menuItems = menuItems?.ToList() ?? throw new ArgumentNullException(nameof(menuItems));
    }

    /// <inheritdoc />
    public IReadOnlyList<MenuItemInfo> MenuItems => _menuItems;

    /// <inheritdoc />
    public override void Register(IServiceCollection services)
    {
        // Menu items don't need DI registration - they're discovered via IPluginManager
        // The menu provider will query plugins for their menu features
    }
}
