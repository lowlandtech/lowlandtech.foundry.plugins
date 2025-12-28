using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Core.Security;

/// <summary>
/// Manages trust relationships between peers.
/// </summary>
public interface ITrustManager
{
    /// <summary>
    /// Gets the trust level for a peer.
    /// </summary>
    TrustLevel GetTrustLevel(PeerId peerId);

    /// <summary>
    /// Sets the trust level for a peer.
    /// </summary>
    Task SetTrustLevelAsync(PeerId peerId, TrustLevel level);

    /// <summary>
    /// Determines whether a connection should be allowed based on trust.
    /// </summary>
    Task<bool> ShouldAllowConnectionAsync(PeerId peerId);

    /// <summary>
    /// Determines whether a plugin from a peer should be accepted.
    /// </summary>
    Task<bool> ShouldAcceptPluginAsync(PeerId fromPeer, string pluginName);

    /// <summary>
    /// Gets all trust records.
    /// </summary>
    Task<IReadOnlyList<TrustRecord>> GetAllTrustRecordsAsync();

    /// <summary>
    /// Raised when trust level changes for a peer.
    /// </summary>
    event Func<PeerId, TrustLevel, Task>? TrustLevelChanged;
}

/// <summary>
/// Trust levels for peers.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// Unknown peer - first contact, limited permissions.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Identity verified but not explicitly trusted.
    /// </summary>
    Verified = 1,

    /// <summary>
    /// Explicitly trusted by user.
    /// </summary>
    Trusted = 2,

    /// <summary>
    /// Blocked - no communication allowed.
    /// </summary>
    Blocked = -1
}

/// <summary>
/// Record of trust relationship with a peer.
/// </summary>
public sealed record TrustRecord
{
    /// <summary>
    /// The peer's ID.
    /// </summary>
    public required PeerId PeerId { get; init; }

    /// <summary>
    /// The peer's display name (when last seen).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Current trust level.
    /// </summary>
    public required TrustLevel TrustLevel { get; init; }

    /// <summary>
    /// When this trust record was first created.
    /// </summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>
    /// When this peer was last seen.
    /// </summary>
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>
    /// When the trust level was last modified.
    /// </summary>
    public required DateTimeOffset ModifiedAt { get; init; }

    /// <summary>
    /// Optional note about why this trust level was set.
    /// </summary>
    public string? Note { get; init; }
}
