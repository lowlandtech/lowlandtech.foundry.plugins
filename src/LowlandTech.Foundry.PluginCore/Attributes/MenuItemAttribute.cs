using LowlandTech.Foundry.PluginCore.Models;

namespace LowlandTech.Foundry.PluginCore.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class MenuItemAttribute : Attribute
{
    public required string Title { get; set; }
    public string Icon { get; set; } = string.Empty;
    public MenuLocation Location { get; set; } = MenuLocation.Sidebar;
    public int Order { get; set; } = 0;
    public string? ParentMenu { get; set; }
    public string? Group { get; set; }
}
