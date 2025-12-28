using System.Reflection;
using LowlandTech.Foundry.PluginCore.Services;
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
}
