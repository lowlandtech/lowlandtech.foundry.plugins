using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace LowlandTech.Foundry.P2P.Signaling.Hubs;

/// <summary>
/// SignalR hub for WebRTC signaling between peers.
/// </summary>
public sealed class SignalingHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConnectedPeer> _peers = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToPeer = new();

    private readonly ILogger<SignalingHub> _logger;

    public SignalingHub(ILogger<SignalingHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a peer with the signaling server.
    /// </summary>
    public async Task Register(string peerId, string displayName, byte[] publicKey)
    {
        var connectionId = Context.ConnectionId;

        var peer = new ConnectedPeer
        {
            PeerId = peerId,
            DisplayName = displayName,
            PublicKey = publicKey,
            ConnectionId = connectionId,
            ConnectedAt = DateTimeOffset.UtcNow
        };

        _peers[peerId] = peer;
        _connectionToPeer[connectionId] = peerId;

        _logger.LogInformation("Peer registered: {PeerId} ({DisplayName})", peerId, displayName);

        // Notify other peers
        await Clients.Others.SendAsync("PeerJoined", peerId, displayName, publicKey);
    }

    /// <summary>
    /// Sends an SDP offer to a target peer.
    /// </summary>
    public async Task SendOffer(string targetPeerId, string sdp)
    {
        if (!_connectionToPeer.TryGetValue(Context.ConnectionId, out var fromPeerId))
        {
            _logger.LogWarning("Unregistered peer tried to send offer");
            return;
        }

        if (_peers.TryGetValue(targetPeerId, out var targetPeer))
        {
            _logger.LogDebug("Relaying offer from {From} to {To}", fromPeerId, targetPeerId);
            await Clients.Client(targetPeer.ConnectionId).SendAsync("ReceiveOffer", fromPeerId, sdp);
        }
        else
        {
            _logger.LogWarning("Target peer not found: {PeerId}", targetPeerId);
        }
    }

    /// <summary>
    /// Sends an SDP answer to a target peer.
    /// </summary>
    public async Task SendAnswer(string targetPeerId, string sdp)
    {
        if (!_connectionToPeer.TryGetValue(Context.ConnectionId, out var fromPeerId))
        {
            _logger.LogWarning("Unregistered peer tried to send answer");
            return;
        }

        if (_peers.TryGetValue(targetPeerId, out var targetPeer))
        {
            _logger.LogDebug("Relaying answer from {From} to {To}", fromPeerId, targetPeerId);
            await Clients.Client(targetPeer.ConnectionId).SendAsync("ReceiveAnswer", fromPeerId, sdp);
        }
        else
        {
            _logger.LogWarning("Target peer not found: {PeerId}", targetPeerId);
        }
    }

    /// <summary>
    /// Sends an ICE candidate to a target peer.
    /// </summary>
    public async Task SendIceCandidate(string targetPeerId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (!_connectionToPeer.TryGetValue(Context.ConnectionId, out var fromPeerId))
        {
            _logger.LogWarning("Unregistered peer tried to send ICE candidate");
            return;
        }

        if (_peers.TryGetValue(targetPeerId, out var targetPeer))
        {
            await Clients.Client(targetPeer.ConnectionId)
                .SendAsync("ReceiveIceCandidate", fromPeerId, candidate, sdpMid, sdpMLineIndex);
        }
    }

    /// <summary>
    /// Gets the list of currently online peers.
    /// </summary>
    public Task<List<OnlinePeerDto>> GetOnlinePeers()
    {
        _connectionToPeer.TryGetValue(Context.ConnectionId, out var currentPeerId);

        var peers = _peers.Values
            .Where(p => p.PeerId != currentPeerId) // Exclude self
            .Select(p => new OnlinePeerDto(p.PeerId, p.DisplayName, p.PublicKey))
            .ToList();

        return Task.FromResult(peers);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionToPeer.TryRemove(Context.ConnectionId, out var peerId))
        {
            if (_peers.TryRemove(peerId, out var peer))
            {
                _logger.LogInformation("Peer disconnected: {PeerId} ({DisplayName})",
                    peerId, peer.DisplayName);

                // Notify other peers
                await Clients.Others.SendAsync("PeerLeft", peerId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}

internal sealed class ConnectedPeer
{
    public required string PeerId { get; init; }
    public required string DisplayName { get; init; }
    public required byte[] PublicKey { get; init; }
    public required string ConnectionId { get; init; }
    public DateTimeOffset ConnectedAt { get; init; }
}

public sealed record OnlinePeerDto(string PeerId, string DisplayName, byte[] PublicKey);
