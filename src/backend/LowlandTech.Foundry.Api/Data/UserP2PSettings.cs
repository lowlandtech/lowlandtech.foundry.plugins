namespace LowlandTech.Foundry.Api.Data;

/// <summary>
/// User-specific P2P collaboration settings stored in the database.
/// </summary>
public class UserP2PSettings
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the user.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// The user's P2P peer ID (public key hash).
    /// </summary>
    public string? PeerId { get; set; }

    /// <summary>
    /// The user's P2P display name (may differ from account display name).
    /// </summary>
    public string? P2PDisplayName { get; set; }

    /// <summary>
    /// Current presence status (Online, Away, Busy, Invisible, Offline).
    /// </summary>
    public string Status { get; set; } = "Online";

    /// <summary>
    /// Custom status message.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Whether to enable LAN discovery via mDNS.
    /// </summary>
    public bool EnableLanDiscovery { get; set; } = true;

    /// <summary>
    /// Whether to enable WAN discovery via signaling server.
    /// </summary>
    public bool EnableWanDiscovery { get; set; } = true;

    /// <summary>
    /// Whether to require cryptographic verification of peers.
    /// </summary>
    public bool RequirePeerVerification { get; set; }

    /// <summary>
    /// Whether to auto-accept connections from trusted peers.
    /// </summary>
    public bool AutoAcceptTrustedPeers { get; set; } = true;

    /// <summary>
    /// Whether to enable P2P plugin sharing.
    /// </summary>
    public bool EnablePluginSharing { get; set; } = true;

    /// <summary>
    /// Whether to auto-download plugins from trusted peers.
    /// </summary>
    public bool AutoDownloadFromTrusted { get; set; }

    /// <summary>
    /// Whether to require signed plugins.
    /// </summary>
    public bool RequireSignedPlugins { get; set; } = true;

    /// <summary>
    /// JSON-serialized list of trusted peer IDs.
    /// </summary>
    public string? TrustedPeerIds { get; set; }

    /// <summary>
    /// JSON-serialized list of blocked peer IDs.
    /// </summary>
    public string? BlockedPeerIds { get; set; }

    /// <summary>
    /// When the settings were created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the settings were last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
