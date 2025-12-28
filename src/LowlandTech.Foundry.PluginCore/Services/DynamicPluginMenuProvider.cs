using System.Reflection;
using LowlandTech.Foundry.PluginCore.Attributes;
using LowlandTech.Foundry.PluginCore.Catalogs;
using LowlandTech.Foundry.PluginCore.Models;
using Microsoft.AspNetCore.Components;

namespace LowlandTech.Foundry.PluginCore.Services;

/// <summary>
/// Plugin menu provider that discovers menus from dynamic plugin catalogs
/// </summary>
public class DynamicPluginMenuProvider : IPluginMenuProvider
{
    private readonly IPluginCatalog _catalog;
    private readonly IEnumerable<Assembly> _additionalAssemblies;
    private List<MenuItemInfo>? _menuItems;
    private List<MenuItemInfo>? _sidebarItems;
    private List<MenuItemInfo>? _topbarItems;
    private readonly object _lock = new();

    public DynamicPluginMenuProvider(IPluginCatalog catalog, IEnumerable<Assembly>? additionalAssemblies = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _additionalAssemblies = additionalAssemblies ?? [];
    }

    public IReadOnlyList<MenuItemInfo> GetSidebarMenuItems()
    {
        EnsureInitialized();
        return _sidebarItems!;
    }

    public IReadOnlyList<MenuItemInfo> GetTopbarMenuItems()
    {
        EnsureInitialized();
        return _topbarItems!;
    }

    public IReadOnlyList<MenuItemInfo> GetAllMenuItems()
    {
        EnsureInitialized();
        return _menuItems!;
    }

    private void EnsureInitialized()
    {
        if (_menuItems != null) return;

        lock (_lock)
        {
            if (_menuItems != null) return;

            var assemblies = GetAllAssemblies();
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
    }

    private HashSet<Assembly> GetAllAssemblies()
    {
        var assemblies = new HashSet<Assembly>();

        // Add assemblies from the catalog
        if (_catalog.IsInitialized)
        {
            foreach (var plugin in _catalog.GetPlugins())
            {
                assemblies.Add(plugin.Assembly);
            }
        }

        // Add additional directly-referenced assemblies
        foreach (var assembly in _additionalAssemblies)
        {
            assemblies.Add(assembly);
        }

        // Add assemblies from AppDomain
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !IsSystemAssembly(assembly))
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

        var parentNames = flatItems
            .Where(i => !string.IsNullOrEmpty(i.ParentMenu))
            .Select(i => i.ParentMenu!)
            .Distinct()
            .ToHashSet();

        foreach (var parentName in parentNames)
        {
            var existingParent = flatItems.FirstOrDefault(i => i.Title == parentName && string.IsNullOrEmpty(i.ParentMenu));

            if (existingParent != null)
            {
                parentGroups[parentName] = existingParent;
            }
            else
            {
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

        foreach (var item in flatItems)
        {
            if (!string.IsNullOrEmpty(item.ParentMenu) && parentGroups.TryGetValue(item.ParentMenu, out var parent))
            {
                parent.Children.Add(item);
            }
        }

        foreach (var parent in parentGroups.Values)
        {
            parent.Children = parent.Children
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Title)
                .ToList();
        }

        foreach (var item in flatItems.Where(i => string.IsNullOrEmpty(i.ParentMenu)))
        {
            if (!parentGroups.ContainsKey(item.Title))
            {
                result.Add(item);
            }
        }

        result.AddRange(parentGroups.Values.Where(p => !flatItems.Any(f => f.Title == p.Title && string.IsNullOrEmpty(f.ParentMenu))));
        result.AddRange(parentGroups.Values.Where(p => flatItems.Any(f => f.Title == p.Title && string.IsNullOrEmpty(f.ParentMenu))));

        return result.DistinctBy(r => r.Title).ToList();
    }
}
