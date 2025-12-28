namespace LowlandTech.Foundry.PluginCore.Models;

public class MenuItemInfo
{
    public required string Title { get; set; }
    public required string Route { get; set; }
    public string Icon { get; set; } = string.Empty;
    public MenuLocation Location { get; set; } = MenuLocation.Sidebar;
    public int Order { get; set; }
    public string? ParentMenu { get; set; }
    public string? Group { get; set; }
    public Type? PageType { get; set; }
    public List<MenuItemInfo> Children { get; set; } = [];
}
