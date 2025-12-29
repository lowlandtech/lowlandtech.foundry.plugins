namespace LowlandTech.Foundry.Collaboration.Models;

/// <summary>
/// Represents an invitation to connect from another peer.
/// </summary>
public sealed class PeerInvite
{
    /// <summary>
    /// Unique identifier for this invitation.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Peer ID of the sender.
    /// </summary>
    public string FromPeerId { get; set; } = "";

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string FromDisplayName { get; set; } = "";

    /// <summary>
    /// Optional message from the sender.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// When the invitation was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the invitation expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);

    /// <summary>
    /// Whether the invitation has been viewed.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Whether the invitation is still valid.
    /// </summary>
    public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
}
