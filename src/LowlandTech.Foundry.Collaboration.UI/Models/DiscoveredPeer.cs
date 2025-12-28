namespace LowlandTech.Foundry.Collaboration.UI.Models;

/// <summary>
/// Represents a peer discovered through network scanning.
/// </summary>
public sealed class DiscoveredPeer
{
    /// <summary>
    /// The peer's unique identifier.
    /// </summary>
    public string PeerId { get; set; } = "";

    /// <summary>
    /// Display name of the peer.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Network address of the peer.
    /// </summary>
    public string Address { get; set; } = "";

    /// <summary>
    /// How the peer was discovered.
    /// </summary>
    public DiscoveryMethod Method { get; set; }

    /// <summary>
    /// Trust level with this peer.
    /// </summary>
    public TrustLevel TrustLevel { get; set; } = TrustLevel.Unknown;

    /// <summary>
    /// When the peer was discovered.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether we are currently connecting to this peer.
    /// </summary>
    public bool IsConnecting { get; set; }

    /// <summary>
    /// Whether we are already connected to this peer.
    /// </summary>
    public bool IsConnected { get; set; }
}

/// <summary>
/// How a peer was discovered.
/// </summary>
public enum DiscoveryMethod
{
    /// <summary>
    /// Discovered via mDNS on local network.
    /// </summary>
    LAN,

    /// <summary>
    /// Discovered via signaling server.
    /// </summary>
    Signaling,

    /// <summary>
    /// Manually entered peer ID.
    /// </summary>
    Manual,

    /// <summary>
    /// Received invitation.
    /// </summary>
    Invitation
}

/// <summary>
/// Trust level for a peer.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// Unknown/unverified peer.
    /// </summary>
    Unknown,

    /// <summary>
    /// Cryptographically verified identity.
    /// </summary>
    Verified,

    /// <summary>
    /// Explicitly trusted by user.
    /// </summary>
    Trusted,

    /// <summary>
    /// Blocked by user.
    /// </summary>
    Blocked
}
