using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.WebRTC.Signaling;

/// <summary>
/// Channel for exchanging WebRTC signaling messages between peers.
/// </summary>
public interface ISignalingChannel : IAsyncDisposable
{
    /// <summary>
    /// Local peer ID.
    /// </summary>
    PeerId LocalPeerId { get; }

    /// <summary>
    /// Whether the signaling channel is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised when a signaling message is received.
    /// </summary>
    event Func<SignalingMessage, Task>? MessageReceived;

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    event Func<bool, Task>? ConnectionStateChanged;

    /// <summary>
    /// Connects to the signaling server.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the signaling server.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Sends an SDP offer to a peer.
    /// </summary>
    Task SendOfferAsync(PeerId targetPeer, string sdp);

    /// <summary>
    /// Sends an SDP answer to a peer.
    /// </summary>
    Task SendAnswerAsync(PeerId targetPeer, string sdp);

    /// <summary>
    /// Sends an ICE candidate to a peer.
    /// </summary>
    Task SendIceCandidateAsync(PeerId targetPeer, string candidate, string? sdpMid, int? sdpMLineIndex);

    /// <summary>
    /// Gets the list of online peers.
    /// </summary>
    Task<IReadOnlyList<PeerInfo>> GetOnlinePeersAsync();
}

/// <summary>
/// Types of signaling messages.
/// </summary>
public enum SignalingMessageType
{
    Offer,
    Answer,
    IceCandidate,
    PeerJoined,
    PeerLeft
}

/// <summary>
/// A signaling message received from a peer.
/// </summary>
public abstract record SignalingMessage
{
    public required PeerId FromPeerId { get; init; }
    public abstract SignalingMessageType Type { get; }
}

public sealed record OfferMessage : SignalingMessage
{
    public required string Sdp { get; init; }
    public override SignalingMessageType Type => SignalingMessageType.Offer;
}

public sealed record AnswerMessage : SignalingMessage
{
    public required string Sdp { get; init; }
    public override SignalingMessageType Type => SignalingMessageType.Answer;
}

public sealed record IceCandidateMessage : SignalingMessage
{
    public required string Candidate { get; init; }
    public string? SdpMid { get; init; }
    public int? SdpMLineIndex { get; init; }
    public override SignalingMessageType Type => SignalingMessageType.IceCandidate;
}

public sealed record PeerJoinedMessage : SignalingMessage
{
    public required PeerInfo Peer { get; init; }
    public override SignalingMessageType Type => SignalingMessageType.PeerJoined;
}

public sealed record PeerLeftMessage : SignalingMessage
{
    public override SignalingMessageType Type => SignalingMessageType.PeerLeft;
}
