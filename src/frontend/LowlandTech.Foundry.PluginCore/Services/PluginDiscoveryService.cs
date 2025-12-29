using System.Reflection;
using LowlandTech.Foundry.PluginCore.Attributes;
using LowlandTech.Foundry.PluginCore.Models;
using Microsoft.AspNetCore.Components;

namespace LowlandTech.Foundry.PluginCore.Services;

public class PluginDiscoveryService : IPluginMenuProvider
{
    private readonly List<MenuItemInfo> _menuItems;
    private readonly List<MenuItemInfo> _sidebarItems;
    private readonly List<MenuItemInfo> _topbarItems;

    public PluginDiscoveryService(IEnumerable<Assembly>? additionalAssemblies = null)
    {
        var assemblies = GetPluginAssemblies(additionalAssemblies);
        _menuItems = DiscoverMenuItems(assemblies);

        var hierarchicalItems = BuildMenuHierarchy(_menuItems);
        _sidebarItems = hierarchicalItems
            .Where(m => m.Location == MenuLocation.Sidebar)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title)
            .ToList();

        _topbarItems = hierarchicalItems
            .Where(m => m.Location == MenuLocation.Topbar)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title)
            .ToList();
    }

    public IReadOnlyList<MenuItemInfo> GetSidebarMenuItems() => _sidebarItems;
    public IReadOnlyList<MenuItemInfo> GetTopbarMenuItems() => _topbarItems;
    public IReadOnlyList<MenuItemInfo> GetAllMenuItems() => _menuItems;

    private static IEnumerable<Assembly> GetPluginAssemblies(IEnumerable<Assembly>? additionalAssemblies)
    {
        var assemblies = new HashSet<Assembly>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !IsSystemAssembly(assembly))
            {
                assemblies.Add(assembly);
            }
        }

        if (additionalAssemblies != null)
        {
            foreach (var assembly in additionalAssemblies)
            {
                assemblies.Add(assembly);
            }
        }

        return assemblies;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;
        return name.StartsWith("System.", StringComparison.Ordinal) ||
               name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
               name.StartsWith("netstandard", StringComparison.Ordinal) ||
               name.StartsWith("mscorlib", StringComparison.Ordinal);
    }

    private static List<MenuItemInfo> DiscoverMenuItems(IEnumerable<Assembly> assemblies)
    {
        var items = new List<MenuItemInfo>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract);

                foreach (var type in types)
                {
                    var menuAttributes = type.GetCustomAttributes<MenuItemAttribute>();
                    var routeAttribute = type.GetCustomAttribute<RouteAttribute>();

                    if (!menuAttributes.Any() || routeAttribute == null)
                        continue;

                    foreach (var menuAttr in menuAttributes)
                    {
                        items.Add(new MenuItemInfo
                        {
                            Title = menuAttr.Title,
                            Route = routeAttribute.Template,
                            Icon = menuAttr.Icon,
                            Location = menuAttr.Location,
                            Order = menuAttr.Order,
                            ParentMenu = menuAttr.ParentMenu,
                            Group = menuAttr.Group,
                            PageType = type
                        });
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
            }
        }

        return items;
    }

    private static List<MenuItemInfo> BuildMenuHierarchy(List<MenuItemInfo> flatItems)
    {
        var result = new List<MenuItemInfo>();
        var parentGroups = new Dictionary<string, MenuItemInfo>();

        // First pass: identify all items that are parents
        var parentNames = flatItems
            .Where(i => !string.IsNullOrEmpty(i.ParentMenu))
            .Select(i => i.ParentMenu!)
            .Distinct()
            .ToHashSet();

        // Second pass: create parent containers for items that reference non-existent parents
        foreach (var parentName in parentNames)
        {
            var existingParent = flatItems.FirstOrDefault(i => i.Title == parentName && string.IsNullOrEmpty(i.ParentMenu));

            if (existingParent != null)
            {
                parentGroups[parentName] = existingParent;
            }
            else
            {
                // Create a virtual parent
                var childWithParent = flatItems.First(i => i.ParentMenu == parentName);
                var virtualParent = new MenuItemInfo
                {
                    Title = parentName,
                    Route = "#",
                    Location = childWithParent.Location,
                    Order = childWithParent.Order - 1,
                    Icon = childWithParent.Icon
                };
                parentGroups[parentName] = virtualParent;
            }
        }

        // Third pass: assign children to parents
        foreach (var item in flatItems)
        {
            if (!string.IsNullOrEmpty(item.ParentMenu) && parentGroups.TryGetValue(item.ParentMenu, out var parent))
            {
                parent.Children.Add(item);
            }
        }

        // Sort children
        foreach (var parent in parentGroups.Values)
        {
            parent.Children = parent.Children
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Title)
                .ToList();
        }

        // Fourth pass: build final list (top-level items only)
        foreach (var item in flatItems.Where(i => string.IsNullOrEmpty(i.ParentMenu)))
        {
            if (!parentGroups.ContainsKey(item.Title))
            {
                result.Add(item);
            }
        }

        // Add parent groups
        result.AddRange(parentGroups.Values.Where(p => !flatItems.Any(f => f.Title == p.Title && string.IsNullOrEmpty(f.ParentMenu))));
        result.AddRange(parentGroups.Values.Where(p => flatItems.Any(f => f.Title == p.Title && string.IsNullOrEmpty(f.ParentMenu))));

        return result.DistinctBy(r => r.Title).ToList();
    }
}
