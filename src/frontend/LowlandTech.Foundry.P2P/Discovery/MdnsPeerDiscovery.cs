using System.Collections.Concurrent;
using System.Net;
using System.Text;
using LowlandTech.Foundry.P2P.Identity;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Discovery;

/// <summary>
/// mDNS-based peer discovery for local network (LAN).
/// Uses the _foundry._tcp service type for peer announcements.
/// </summary>
public sealed class MdnsPeerDiscovery : IPeerDiscovery
{
    private const string ServiceType = "_foundry._tcp.local";
    private const int DefaultPort = 9999;

    private readonly MulticastService _mdns;
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly ILogger<MdnsPeerDiscovery>? _logger;
    private readonly ConcurrentDictionary<PeerId, PeerInfo> _knownPeers = new();
    private readonly ConcurrentDictionary<string, PeerId> _serviceNameToPeerId = new();
    private PeerInfo? _localPeer;
    private ServiceProfile? _localService;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event Func<PeerInfo, Task>? PeerDiscovered;
    public event Func<PeerId, Task>? PeerLost;

    public MdnsPeerDiscovery(ILogger<MdnsPeerDiscovery>? logger = null)
    {
        _logger = logger;
        _mdns = new MulticastService();
        _serviceDiscovery = new ServiceDiscovery(_mdns);

        SetupHandlers();
    }

    private void SetupHandlers()
    {
        _serviceDiscovery.ServiceInstanceDiscovered += async (sender, args) =>
        {
            try
            {
                await HandleServiceDiscoveredAsync(args);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling discovered service");
            }
        };

        _serviceDiscovery.ServiceInstanceShutdown += async (sender, args) =>
        {
            try
            {
                await HandleServiceShutdownAsync(args);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling service shutdown");
            }
        };

        _mdns.NetworkInterfaceDiscovered += (sender, args) =>
        {
            _logger?.LogDebug("Network interface discovered: {Interfaces}",
                string.Join(", ", args.NetworkInterfaces.Select(n => n.Name)));
        };
    }

    private async Task HandleServiceDiscoveredAsync(ServiceInstanceDiscoveryEventArgs args)
    {
        var serviceName = args.ServiceInstanceName.ToString();

        // Ignore our own service
        if (_localService != null && serviceName == _localService.FullyQualifiedName.ToString())
            return;

        _logger?.LogDebug("Service discovered: {ServiceName}", serviceName);

        // Query for more details about this service
        var message = args.Message;

        // Look for TXT records with peer info
        var txtRecords = message.AdditionalRecords
            .OfType<TXTRecord>()
            .Where(r => r.Name == args.ServiceInstanceName)
            .ToList();

        var srvRecords = message.AdditionalRecords
            .OfType<SRVRecord>()
            .Where(r => r.Name == args.ServiceInstanceName)
            .ToList();

        var aRecords = message.AdditionalRecords
            .OfType<ARecord>()
            .ToList();

        if (txtRecords.Count == 0 || srvRecords.Count == 0)
        {
            _logger?.LogDebug("Incomplete service record, waiting for more info");
            return;
        }

        // Parse peer info from TXT records
        var txtData = new Dictionary<string, string>();
        foreach (var txt in txtRecords)
        {
            foreach (var entry in txt.Strings)
            {
                var parts = entry.Split('=', 2);
                if (parts.Length == 2)
                {
                    txtData[parts[0]] = parts[1];
                }
            }
        }

        if (!txtData.TryGetValue("peerId", out var peerIdStr) ||
            !txtData.TryGetValue("publicKey", out var publicKeyB64) ||
            !txtData.TryGetValue("displayName", out var displayName))
        {
            _logger?.LogDebug("Missing required TXT records");
            return;
        }

        if (!PeerId.TryParse(peerIdStr, out var peerId))
        {
            _logger?.LogWarning("Invalid peerId in service record: {PeerId}", peerIdStr);
            return;
        }

        var publicKey = Convert.FromBase64String(publicKeyB64);
        var srv = srvRecords.First();

        // Get endpoints
        var endpoints = new List<string>();
        foreach (var a in aRecords)
        {
            endpoints.Add($"{a.Address}:{srv.Port}");
        }

        var mode = txtData.TryGetValue("mode", out var modeStr) &&
                   Enum.TryParse<NodeMode>(modeStr, out var parsedMode)
            ? parsedMode
            : NodeMode.Client;

        var peerInfo = new PeerInfo
        {
            Id = peerId,
            PublicKey = publicKey,
            DisplayName = displayName,
            Endpoints = endpoints.ToArray(),
            Mode = mode,
            AvatarUrl = txtData.TryGetValue("avatarUrl", out var avatar) ? avatar : null,
            LastSeen = DateTimeOffset.UtcNow
        };

        _serviceNameToPeerId[serviceName] = peerId;

        if (_knownPeers.TryAdd(peerId, peerInfo))
        {
            _logger?.LogInformation("Discovered peer: {PeerId} ({DisplayName}) at {Endpoints}",
                peerId, displayName, string.Join(", ", endpoints));

            if (PeerDiscovered != null)
                await PeerDiscovered.Invoke(peerInfo);
        }
        else
        {
            // Update existing peer info
            _knownPeers[peerId] = peerInfo;
        }
    }

    private async Task HandleServiceShutdownAsync(ServiceInstanceShutdownEventArgs args)
    {
        var serviceName = args.ServiceInstanceName.ToString();

        if (_serviceNameToPeerId.TryRemove(serviceName, out var peerId))
        {
            if (_knownPeers.TryRemove(peerId, out var peerInfo))
            {
                _logger?.LogInformation("Peer left: {PeerId} ({DisplayName})", peerId, peerInfo.DisplayName);

                if (PeerLost != null)
                    await PeerLost.Invoke(peerId);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;

        _logger?.LogInformation("Starting mDNS peer discovery...");

        _mdns.Start();

        // Start querying for foundry services
        _serviceDiscovery.QueryServiceInstances(ServiceType);

        IsRunning = true;

        _logger?.LogInformation("mDNS peer discovery started");

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _logger?.LogInformation("Stopping mDNS peer discovery...");

        await UnannounceAsync();
        _mdns.Stop();

        IsRunning = false;

        _logger?.LogInformation("mDNS peer discovery stopped");
    }

    public Task<IReadOnlyList<PeerInfo>> GetKnownPeersAsync()
    {
        var peers = _knownPeers.Values.ToList();
        return Task.FromResult<IReadOnlyList<PeerInfo>>(peers);
    }

    public async Task AnnounceAsync(PeerInfo localPeer)
    {
        _localPeer = localPeer;

        // Create service profile
        var instanceName = $"foundry-{localPeer.Id.ToBase58()[..8]}";

        _localService = new ServiceProfile(
            instanceName,
            ServiceType,
            DefaultPort);

        // Add TXT records with peer info
        _localService.AddProperty("peerId", localPeer.Id.ToBase58());
        _localService.AddProperty("publicKey", Convert.ToBase64String(localPeer.PublicKey));
        _localService.AddProperty("displayName", localPeer.DisplayName);
        _localService.AddProperty("mode", localPeer.Mode.ToString());

        if (!string.IsNullOrEmpty(localPeer.AvatarUrl))
        {
            _localService.AddProperty("avatarUrl", localPeer.AvatarUrl);
        }

        // Advertise the service
        _serviceDiscovery.Advertise(_localService);

        _logger?.LogInformation("Announced local peer on mDNS: {DisplayName}", localPeer.DisplayName);

        await Task.CompletedTask;
    }

    public async Task UnannounceAsync()
    {
        if (_localService != null)
        {
            _serviceDiscovery.Unadvertise(_localService);
            _localService = null;
            _localPeer = null;

            _logger?.LogInformation("Unannounced local peer from mDNS");
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();

        _serviceDiscovery.Dispose();
        _mdns.Dispose();
    }
}
