using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Core.Discovery;

/// <summary>
/// Interface for discovering peers on the network.
/// </summary>
public interface IPeerDiscovery : IAsyncDisposable
{
    /// <summary>
    /// Whether the discovery service is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised when a new peer is discovered.
    /// </summary>
    event Func<PeerInfo, Task>? PeerDiscovered;

    /// <summary>
    /// Raised when a previously discovered peer is no longer available.
    /// </summary>
    event Func<PeerId, Task>? PeerLost;

    /// <summary>
    /// Starts the discovery service.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the discovery service.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets all currently known peers.
    /// </summary>
    Task<IReadOnlyList<PeerInfo>> GetKnownPeersAsync();

    /// <summary>
    /// Announces the local peer to the network.
    /// </summary>
    Task AnnounceAsync(PeerInfo localPeer);

    /// <summary>
    /// Removes the local peer announcement from the network.
    /// </summary>
    Task UnannounceAsync();
}
