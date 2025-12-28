using System.Reflection;
using LowlandTech.Foundry.PluginCore.Catalogs;
using LowlandTech.Foundry.PluginCore.Configuration;
using LowlandTech.Foundry.PluginCore.Services;
using LowlandTech.Foundry.PluginCore.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.PluginCore.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the plugin system with dynamic loading from configuration
    /// </summary>
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] additionalAssemblies)
    {
        var options = new PluginOptions();
        configuration.GetSection(PluginOptions.SectionName).Bind(options);

        return services.AddPluginSystem(options, additionalAssemblies);
    }

    /// <summary>
    /// Adds the plugin system with explicit options
    /// </summary>
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        PluginOptions options,
        params Assembly[] additionalAssemblies)
    {
        // Register the composite catalog as a singleton
        services.AddSingleton<IPluginCatalog>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("PluginCatalog");
            var catalogs = new List<IPluginCatalog>();

            // Add assembly catalogs for directly referenced assemblies
            foreach (var assembly in additionalAssemblies)
            {
                var assemblyCatalog = new AssemblyPluginCatalog(
                    assembly,
                    CreateBlazorComponentCriteria(),
                    logger: logger);
                catalogs.Add(assemblyCatalog);
            }

            // Add folder catalogs from configuration
            foreach (var folder in options.Folders)
            {
                var folderOptions = new FolderPluginCatalogOptions
                {
                    IncludeSubfolders = folder.IncludeSubfolders,
                    SearchPatterns = folder.SearchPatterns,
                    TypeFinderCriteria = CreateBlazorComponentCriteria()
                };

                var folderCatalog = new FolderPluginCatalog(folder.Path, folderOptions, logger);
                catalogs.Add(folderCatalog);
            }

            // Add NuGet package catalogs from configuration
            foreach (var package in options.NuGetPackages)
            {
                var nugetOptions = new NuGetPluginCatalogOptions
                {
                    PackagesFolder = options.PackagesFolder,
                    PackageSources = options.NuGetFeeds.Count > 0 ? options.NuGetFeeds : null,
                    IncludePrerelease = package.IncludePrerelease ?? options.IncludePrerelease,
                    TargetFramework = options.TargetFramework,
                    TypeFinderCriteria = CreateBlazorComponentCriteria()
                };

                var nugetCatalog = new NuGetPluginCatalog(
                    package.PackageId,
                    package.Version,
                    nugetOptions,
                    logger);

                catalogs.Add(nugetCatalog);
            }

            return new CompositePluginCatalog(catalogs);
        });

        // Register the plugin menu provider
        services.AddSingleton<IPluginMenuProvider>(sp =>
        {
            var catalog = sp.GetRequiredService<IPluginCatalog>();
            return new DynamicPluginMenuProvider(catalog, additionalAssemblies);
        });

        // Register a hosted service to initialize catalogs
        services.AddHostedService<PluginCatalogInitializer>();

        return services;
    }

    /// <summary>
    /// Adds the plugin system with hardcoded assemblies (legacy support)
    /// </summary>
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        params Assembly[] pluginAssemblies)
    {
        services.AddSingleton<IPluginMenuProvider>(sp =>
            new PluginDiscoveryService(pluginAssemblies));

        return services;
    }

    /// <summary>
    /// Adds a plugin catalog to the service collection
    /// </summary>
    public static IServiceCollection AddPluginCatalog(
        this IServiceCollection services,
        IPluginCatalog catalog)
    {
        services.AddSingleton(catalog);
        return services;
    }

    /// <summary>
    /// Adds a folder plugin catalog
    /// </summary>
    public static IServiceCollection AddPluginFolder(
        this IServiceCollection services,
        string folderPath,
        Action<FolderPluginCatalogOptions>? configure = null)
    {
        var options = new FolderPluginCatalogOptions
        {
            TypeFinderCriteria = CreateBlazorComponentCriteria()
        };

        configure?.Invoke(options);

        services.AddSingleton<IPluginCatalog>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<FolderPluginCatalog>();
            return new FolderPluginCatalog(folderPath, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a NuGet plugin catalog
    /// </summary>
    public static IServiceCollection AddPluginFromNuGet(
        this IServiceCollection services,
        string packageId,
        string? version = null,
        Action<NuGetPluginCatalogOptions>? configure = null)
    {
        var options = new NuGetPluginCatalogOptions
        {
            TypeFinderCriteria = CreateBlazorComponentCriteria()
        };

        configure?.Invoke(options);

        services.AddSingleton<IPluginCatalog>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<NuGetPluginCatalog>();
            return new NuGetPluginCatalog(packageId, version, options, logger);
        });

        return services;
    }

    public static IServiceCollection AddFoundryTheming(
        this IServiceCollection services,
        Action<ThemingOptions>? configure = null)
    {
        var options = new ThemingOptions();
        configure?.Invoke(options);

        // Register all themes
        foreach (var theme in options.Themes)
        {
            services.AddSingleton(theme);
        }

        // If no themes registered, add default
        if (options.Themes.Count == 0)
        {
            services.AddSingleton<ITheme, DefaultTheme>();
        }

        // Register theme service
        services.AddScoped<IThemeService>(sp =>
        {
            var themes = sp.GetServices<ITheme>();
            var storageProvider = sp.GetService<IThemeStorageProvider>();
            return new ThemeService(themes, storageProvider, options.DefaultToDarkMode);
        });

        return services;
    }

    public static IServiceCollection AddTheme<TTheme>(this IServiceCollection services)
        where TTheme : class, ITheme
    {
        services.AddSingleton<ITheme, TTheme>();
        return services;
    }

    /// <summary>
    /// Creates criteria for finding Blazor components with menu attributes
    /// </summary>
    private static TypeFinderCriteria CreateBlazorComponentCriteria()
    {
        return TypeFinderCriteriaBuilder.Create()
            .IsAbstract(false)
            .IsInterface(false)
            .Query(type =>
            {
                // Check if it's a Blazor component with MenuItem attribute
                var hasMenuItemAttribute = type.GetCustomAttributes()
                    .Any(a => a.GetType().Name == "MenuItemAttribute");

                var isBlazorComponent = type.GetInterfaces()
                    .Any(i => i.FullName?.Contains("IComponent") == true);

                return hasMenuItemAttribute || isBlazorComponent;
            })
            .Build();
    }
}

public class ThemingOptions
{
    public bool DefaultToDarkMode { get; set; }
    public List<ITheme> Themes { get; } = [];

    public ThemingOptions AddTheme<TTheme>() where TTheme : ITheme, new()
    {
        Themes.Add(new TTheme());
        return this;
    }

    public ThemingOptions AddTheme(ITheme theme)
    {
        Themes.Add(theme);
        return this;
    }
}
