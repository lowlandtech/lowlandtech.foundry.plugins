using LowlandTech.Foundry.Api.Data;
using LowlandTech.Foundry.PluginCore.Hosting;

namespace LowlandTech.Foundry.Api.Services;

/// <summary>
/// Factory for creating and retrieving plugin hosts based on hosting mode.
/// </summary>
public class PluginHostFactory : IPluginHostFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IPluginHost> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public PluginHostFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IPluginHost GetHost(string hostingMode)
    {
        if (_hosts.TryGetValue(hostingMode, out var cachedHost))
        {
            return cachedHost;
        }

        IPluginHost host = hostingMode switch
        {
            PluginHostingMode.Container => _serviceProvider.GetRequiredService<ContainerPluginHost>(),
            PluginHostingMode.InProcess => _serviceProvider.GetRequiredService<InProcessPluginHost>(),
            PluginHostingMode.Desktop => _serviceProvider.GetRequiredService<InProcessPluginHost>(), // Same as InProcess
            _ => throw new ArgumentException($"Unknown hosting mode: {hostingMode}", nameof(hostingMode))
        };

        _hosts[hostingMode] = host;
        return host;
    }

    /// <inheritdoc />
    public IEnumerable<IPluginHost> GetAllHosts()
    {
        // Ensure all hosts are created
        GetHost(PluginHostingMode.Container);
        GetHost(PluginHostingMode.InProcess);

        return _hosts.Values;
    }
}
