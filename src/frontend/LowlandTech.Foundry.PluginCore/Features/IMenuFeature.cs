using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Models;

namespace LowlandTech.Foundry.PluginCore.Features;

/// <summary>
/// A plugin feature that provides menu items for navigation.
/// </summary>
public interface IMenuFeature : IPluginFeature
{
    /// <summary>
    /// Gets the menu items provided by this feature.
    /// </summary>
    IReadOnlyList<MenuItemInfo> MenuItems { get; }
}
