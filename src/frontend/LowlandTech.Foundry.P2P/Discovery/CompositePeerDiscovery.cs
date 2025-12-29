using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Identity;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Discovery;

/// <summary>
/// Combines multiple peer discovery mechanisms (e.g., mDNS for LAN and SignalR for WAN).
/// </summary>
public sealed class CompositePeerDiscovery : IPeerDiscovery
{
    private readonly IReadOnlyList<IPeerDiscovery> _discoveries;
    private readonly ILogger<CompositePeerDiscovery>? _logger;
    private readonly ConcurrentDictionary<PeerId, PeerInfo> _knownPeers = new();
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event Func<PeerInfo, Task>? PeerDiscovered;
    public event Func<PeerId, Task>? PeerLost;

    public CompositePeerDiscovery(
        IEnumerable<IPeerDiscovery> discoveries,
        ILogger<CompositePeerDiscovery>? logger = null)
    {
        _discoveries = discoveries?.ToList() ?? throw new ArgumentNullException(nameof(discoveries));
        _logger = logger;

        foreach (var discovery in _discoveries)
        {
            discovery.PeerDiscovered += HandlePeerDiscoveredAsync;
            discovery.PeerLost += HandlePeerLostAsync;
        }
    }

    /// <summary>
    /// Creates a composite discovery with mDNS (LAN) and SignalR (WAN) discovery.
    /// </summary>
    public static CompositePeerDiscovery CreateDefault(
        string? signalingServerUrl = null,
        ILogger<CompositePeerDiscovery>? compositeLogger = null,
        ILogger<MdnsPeerDiscovery>? mdnsLogger = null,
        ILogger<SignalRPeerDiscovery>? signalRLogger = null)
    {
        var discoveries = new List<IPeerDiscovery>
        {
            new MdnsPeerDiscovery(mdnsLogger)
        };

        if (!string.IsNullOrEmpty(signalingServerUrl))
        {
            discoveries.Add(new SignalRPeerDiscovery(signalingServerUrl, signalRLogger));
        }

        return new CompositePeerDiscovery(discoveries, compositeLogger);
    }

    private async Task HandlePeerDiscoveredAsync(PeerInfo peerInfo)
    {
        // Merge with existing peer info if we already know about this peer
        // This can happen if a peer is discovered via multiple mechanisms
        if (_knownPeers.TryGetValue(peerInfo.Id, out var existingPeer))
        {
            // Merge endpoints
            var allEndpoints = existingPeer.Endpoints
                .Concat(peerInfo.Endpoints)
                .Distinct()
                .ToArray();

            peerInfo = peerInfo with
            {
                Endpoints = allEndpoints,
                LastSeen = DateTimeOffset.UtcNow
            };

            _knownPeers[peerInfo.Id] = peerInfo;

            _logger?.LogDebug("Updated peer info: {PeerId} ({DisplayName}) - {EndpointCount} endpoints",
                peerInfo.Id, peerInfo.DisplayName, allEndpoints.Length);
        }
        else
        {
            _knownPeers[peerInfo.Id] = peerInfo;

            _logger?.LogInformation("Discovered peer: {PeerId} ({DisplayName})",
                peerInfo.Id, peerInfo.DisplayName);

            if (PeerDiscovered != null)
                await PeerDiscovered.Invoke(peerInfo);
        }
    }

    private async Task HandlePeerLostAsync(PeerId peerId)
    {
        // Only mark as lost if no other discovery mechanisms still see this peer
        // For simplicity, we'll remove immediately
        // A more sophisticated implementation might wait for all mechanisms to report lost

        if (_knownPeers.TryRemove(peerId, out var peerInfo))
        {
            _logger?.LogInformation("Peer lost: {PeerId} ({DisplayName})",
                peerId, peerInfo.DisplayName);

            if (PeerLost != null)
                await PeerLost.Invoke(peerId);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;

        _logger?.LogInformation("Starting composite peer discovery with {Count} mechanisms...",
            _discoveries.Count);

        var startTasks = _discoveries.Select(d => d.StartAsync(cancellationToken));
        await Task.WhenAll(startTasks);

        IsRunning = true;

        _logger?.LogInformation("Composite peer discovery started");
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _logger?.LogInformation("Stopping composite peer discovery...");

        var stopTasks = _discoveries.Select(d => d.StopAsync());
        await Task.WhenAll(stopTasks);

        _knownPeers.Clear();
        IsRunning = false;

        _logger?.LogInformation("Composite peer discovery stopped");
    }

    public Task<IReadOnlyList<PeerInfo>> GetKnownPeersAsync()
    {
        var peers = _knownPeers.Values.ToList();
        return Task.FromResult<IReadOnlyList<PeerInfo>>(peers);
    }

    public async Task AnnounceAsync(PeerInfo localPeer)
    {
        _logger?.LogInformation("Announcing local peer on all discovery mechanisms...");

        var announceTasks = _discoveries.Select(d => d.AnnounceAsync(localPeer));
        await Task.WhenAll(announceTasks);
    }

    public async Task UnannounceAsync()
    {
        var unannounceTasks = _discoveries.Select(d => d.UnannounceAsync());
        await Task.WhenAll(unannounceTasks);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var discovery in _discoveries)
        {
            discovery.PeerDiscovered -= HandlePeerDiscoveredAsync;
            discovery.PeerLost -= HandlePeerLostAsync;
        }

        var disposeTasks = _discoveries.Select(d => d.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
    }
}
