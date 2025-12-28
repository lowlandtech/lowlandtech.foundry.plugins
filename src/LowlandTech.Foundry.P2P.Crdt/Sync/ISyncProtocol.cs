using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Core.Transport;

namespace LowlandTech.Foundry.P2P.Crdt.Sync;

/// <summary>
/// Protocol for synchronizing CRDTs between peers.
/// </summary>
public interface ISyncProtocol
{
    /// <summary>
    /// Starts synchronization with a peer.
    /// </summary>
    Task StartSyncAsync(PeerConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops synchronization with a peer.
    /// </summary>
    Task StopSyncAsync(PeerId peerId);

    /// <summary>
    /// Requests synchronization for a specific document/CRDT.
    /// </summary>
    Task RequestSyncAsync(PeerId peerId, Guid documentId);

    /// <summary>
    /// Raised when a sync message is received.
    /// </summary>
    event Func<SyncMessage, Task>? MessageReceived;

    /// <summary>
    /// Raised when sync completes for a document.
    /// </summary>
    event Func<PeerId, Guid, Task>? SyncCompleted;
}

/// <summary>
/// Types of sync messages.
/// </summary>
public enum SyncMessageType : byte
{
    /// <summary>Request for state vector from peer.</summary>
    StateVectorRequest = 1,

    /// <summary>Response with current state vector.</summary>
    StateVectorResponse = 2,

    /// <summary>Delta update for a document.</summary>
    Delta = 3,

    /// <summary>Acknowledgment of received delta.</summary>
    Ack = 4,

    /// <summary>Request full sync for a document.</summary>
    FullSyncRequest = 5,

    /// <summary>Error during sync.</summary>
    Error = 6
}

/// <summary>
/// Base class for sync messages.
/// </summary>
public abstract record SyncMessage
{
    /// <summary>Message ID for correlation.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>Sender peer ID.</summary>
    public required PeerId SenderId { get; init; }

    /// <summary>Timestamp of the message.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Type of sync message.</summary>
    public abstract SyncMessageType Type { get; }
}

/// <summary>
/// Request for a peer's state vector for a document.
/// </summary>
public sealed record StateVectorRequest : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public override SyncMessageType Type => SyncMessageType.StateVectorRequest;
}

/// <summary>
/// Response with the current state vector.
/// </summary>
public sealed record StateVectorResponse : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public required byte[] StateVector { get; init; }
    public override SyncMessageType Type => SyncMessageType.StateVectorResponse;
}

/// <summary>
/// Delta update containing changes since a state vector.
/// </summary>
public sealed record DeltaMessage : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public required byte[] Delta { get; init; }
    public required byte[] NewStateVector { get; init; }
    public override SyncMessageType Type => SyncMessageType.Delta;
}

/// <summary>
/// Acknowledgment that a delta was received and applied.
/// </summary>
public sealed record AckMessage : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public required byte[] StateVector { get; init; }
    public override SyncMessageType Type => SyncMessageType.Ack;
}

/// <summary>
/// Request for full synchronization.
/// </summary>
public sealed record FullSyncRequest : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public override SyncMessageType Type => SyncMessageType.FullSyncRequest;
}

/// <summary>
/// Sync error message.
/// </summary>
public sealed record SyncErrorMessage : SyncMessage
{
    public required Guid DocumentId { get; init; }
    public required string ErrorMessage { get; init; }
    public override SyncMessageType Type => SyncMessageType.Error;
}
