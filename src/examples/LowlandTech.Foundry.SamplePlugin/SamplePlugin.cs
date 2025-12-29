using System.Reflection;
using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Attributes;
using LowlandTech.Foundry.PluginCore.Features;
using LowlandTech.Foundry.PluginCore.Models;
using Microsoft.AspNetCore.Components;

namespace LowlandTech.Foundry.SamplePlugin;

/// <summary>
/// Sample plugin demonstrating menu items and page navigation.
/// </summary>
public class SamplePlugin : PluginBase
{
    public override PluginMetadata Metadata => new(
        Id: "lowlandtech.sampleplugin",
        Name: "Sample Plugin",
        Description: "A sample plugin demonstrating dashboard, reports, and settings pages with sidebar and topbar navigation",
        Version: new Version(1, 0, 0),
        Author: "LowlandTech",
        Tags: ["sample", "demo", "navigation"]
    );

    protected override IEnumerable<IPluginFeature> CreateFeatures()
    {
        // Discover menu items from pages in this assembly
        var menuItems = DiscoverMenuItems();

        yield return new MenuFeature(
            this,
            id: "navigation",
            name: "Navigation Menu",
            description: "Provides navigation menu items for the sample plugin pages",
            menuItems: menuItems);
    }

    private List<MenuItemInfo> DiscoverMenuItems()
    {
        var menuItems = new List<MenuItemInfo>();
        var assembly = GetType().Assembly;

        // Find all types with MenuItem attribute
        var pageTypes = assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<MenuItemAttribute>() != null);

        foreach (var pageType in pageTypes)
        {
            var menuAttr = pageType.GetCustomAttribute<MenuItemAttribute>()!;
            var routeAttr = pageType.GetCustomAttribute<RouteAttribute>();

            if (routeAttr == null) continue;

            var menuItem = new MenuItemInfo
            {
                Title = menuAttr.Title,
                Route = routeAttr.Template,
                Icon = menuAttr.Icon,
                Location = menuAttr.Location,
                Order = menuAttr.Order,
                ParentMenu = menuAttr.ParentMenu,
                PageType = pageType
            };

            menuItems.Add(menuItem);
        }

        // Build parent-child relationships
        return BuildMenuHierarchy(menuItems);
    }

    private static List<MenuItemInfo> BuildMenuHierarchy(List<MenuItemInfo> flatItems)
    {
        var result = new List<MenuItemInfo>();
        var parentMap = new Dictionary<string, MenuItemInfo>(StringComparer.OrdinalIgnoreCase);

        // First pass: add root items and build parent map
        foreach (var item in flatItems.OrderBy(i => i.Order))
        {
            if (string.IsNullOrEmpty(item.ParentMenu))
            {
                result.Add(item);
                parentMap[item.Title] = item;
            }
        }

        // Second pass: add children to parents
        foreach (var item in flatItems.Where(i => !string.IsNullOrEmpty(i.ParentMenu)).OrderBy(i => i.Order))
        {
            if (parentMap.TryGetValue(item.ParentMenu!, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                // Parent not found, add as root
                result.Add(item);
            }
        }

        return result.OrderBy(i => i.Order).ToList();
    }
}
