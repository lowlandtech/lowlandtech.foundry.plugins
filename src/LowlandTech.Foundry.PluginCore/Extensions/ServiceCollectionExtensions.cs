using System.Reflection;
using LowlandTech.Foundry.PluginCore.Services;
using LowlandTech.Foundry.PluginCore.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace LowlandTech.Foundry.PluginCore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        params Assembly[] pluginAssemblies)
    {
        services.AddSingleton<IPluginMenuProvider>(sp =>
            new PluginDiscoveryService(pluginAssemblies));

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
        services.AddSingleton<IThemeService>(sp =>
        {
            var themes = sp.GetServices<ITheme>();
            return new ThemeService(themes, options.DefaultToDarkMode);
        });

        return services;
    }

    public static IServiceCollection AddTheme<TTheme>(this IServiceCollection services)
        where TTheme : class, ITheme
    {
        services.AddSingleton<ITheme, TTheme>();
        return services;
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
