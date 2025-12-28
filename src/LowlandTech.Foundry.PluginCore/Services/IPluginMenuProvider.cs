using LowlandTech.Foundry.PluginCore.Models;

namespace LowlandTech.Foundry.PluginCore.Services;

public interface IPluginMenuProvider
{
    IReadOnlyList<MenuItemInfo> GetSidebarMenuItems();
    IReadOnlyList<MenuItemInfo> GetTopbarMenuItems();
    IReadOnlyList<MenuItemInfo> GetAllMenuItems();
}
