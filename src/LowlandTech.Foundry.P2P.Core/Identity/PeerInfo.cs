using MessagePack;

namespace LowlandTech.Foundry.P2P.Core.Identity;

/// <summary>
/// Information about a peer available on the network.
/// </summary>
[MessagePackObject]
public sealed record PeerInfo
{
    /// <summary>
    /// The unique peer identifier.
    /// </summary>
    [Key(0)]
    public required PeerId Id { get; init; }

    /// <summary>
    /// The peer's public key for authentication.
    /// </summary>
    [Key(1)]
    public required byte[] PublicKey { get; init; }

    /// <summary>
    /// Display name of the peer.
    /// </summary>
    [Key(2)]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional avatar URL.
    /// </summary>
    [Key(3)]
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Endpoint addresses where the peer can be reached (IP:port or signaling ID).
    /// </summary>
    [Key(4)]
    public string[] Endpoints { get; set; } = [];

    /// <summary>
    /// Node mode (client, server, hybrid).
    /// </summary>
    [Key(5)]
    public NodeMode Mode { get; set; } = NodeMode.Client;

    /// <summary>
    /// When this peer info was last updated.
    /// </summary>
    [Key(6)]
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional metadata about the peer.
    /// </summary>
    [Key(7)]
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// The operating mode of a P2P node.
/// </summary>
public enum NodeMode
{
    /// <summary>
    /// Client-only mode - connects to other peers but doesn't accept incoming connections.
    /// </summary>
    Client = 0,

    /// <summary>
    /// Server mode - accepts incoming connections and can host sessions for others.
    /// </summary>
    Server = 1,

    /// <summary>
    /// Hybrid mode - both client and server capabilities.
    /// </summary>
    Hybrid = 2
}
