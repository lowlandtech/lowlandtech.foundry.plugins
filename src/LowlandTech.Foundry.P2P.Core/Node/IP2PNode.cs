using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Core.Transport;

namespace LowlandTech.Foundry.P2P.Core.Node;

/// <summary>
/// Represents a P2P network node that can connect to and communicate with other peers.
/// </summary>
public interface IP2PNode : IAsyncDisposable
{
    /// <summary>
    /// This node's identity.
    /// </summary>
    PeerIdentity Identity { get; }

    /// <summary>
    /// Current operating mode of this node.
    /// </summary>
    NodeMode Mode { get; }

    /// <summary>
    /// Whether the node is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Currently connected peers.
    /// </summary>
    IReadOnlyDictionary<PeerId, PeerConnection> ConnectedPeers { get; }

    /// <summary>
    /// Raised when a new peer connects.
    /// </summary>
    event Func<PeerConnection, Task>? PeerConnected;

    /// <summary>
    /// Raised when a peer disconnects.
    /// </summary>
    event Func<PeerId, DisconnectReason, Task>? PeerDisconnected;

    /// <summary>
    /// Raised when a message is received from a peer.
    /// </summary>
    event Func<PeerId, ReadOnlyMemory<byte>, Task>? MessageReceived;

    /// <summary>
    /// Starts the P2P node.
    /// </summary>
    Task StartAsync(NodeMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the P2P node and disconnects all peers.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Switches the node's operating mode.
    /// </summary>
    Task SwitchModeAsync(NodeMode newMode);

    /// <summary>
    /// Connects to a remote peer.
    /// </summary>
    Task<PeerConnection> ConnectAsync(PeerInfo peer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from a specific peer.
    /// </summary>
    Task DisconnectAsync(PeerId peerId);

    /// <summary>
    /// Sends a message to a specific peer.
    /// </summary>
    Task SendAsync(PeerId peerId, ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to all connected peers.
    /// </summary>
    Task BroadcastAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reasons for peer disconnection.
/// </summary>
public enum DisconnectReason
{
    /// <summary>Local node initiated disconnect.</summary>
    LocalDisconnect,

    /// <summary>Remote peer initiated disconnect.</summary>
    RemoteDisconnect,

    /// <summary>Connection timed out.</summary>
    Timeout,

    /// <summary>Connection failed due to network error.</summary>
    NetworkError,

    /// <summary>Authentication failed.</summary>
    AuthenticationFailed,

    /// <summary>Protocol error.</summary>
    ProtocolError,

    /// <summary>Node is shutting down.</summary>
    Shutdown
}
