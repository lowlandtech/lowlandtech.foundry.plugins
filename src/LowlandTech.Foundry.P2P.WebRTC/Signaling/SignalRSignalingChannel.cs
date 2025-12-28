using LowlandTech.Foundry.P2P.Core.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.WebRTC.Signaling;

/// <summary>
/// SignalR-based signaling channel for WebRTC negotiation.
/// </summary>
public sealed class SignalRSignalingChannel : ISignalingChannel
{
    private readonly HubConnection _hubConnection;
    private readonly PeerIdentity _localIdentity;
    private readonly ILogger<SignalRSignalingChannel>? _logger;
    private bool _disposed;

    public PeerId LocalPeerId => _localIdentity.Id;
    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public event Func<SignalingMessage, Task>? MessageReceived;
    public event Func<bool, Task>? ConnectionStateChanged;

    public SignalRSignalingChannel(
        string signalingServerUrl,
        PeerIdentity localIdentity,
        ILogger<SignalRSignalingChannel>? logger = null)
    {
        _localIdentity = localIdentity ?? throw new ArgumentNullException(nameof(localIdentity));
        _logger = logger;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(signalingServerUrl)
            .WithAutomaticReconnect()
            .Build();

        SetupHandlers();
    }

    private void SetupHandlers()
    {
        _hubConnection.On<string, string>("ReceiveOffer", async (fromPeerId, sdp) =>
        {
            _logger?.LogDebug("Received offer from {PeerId}", fromPeerId);

            if (MessageReceived != null && PeerId.TryParse(fromPeerId, out var peerId))
            {
                await MessageReceived.Invoke(new OfferMessage
                {
                    FromPeerId = peerId,
                    Sdp = sdp
                });
            }
        });

        _hubConnection.On<string, string>("ReceiveAnswer", async (fromPeerId, sdp) =>
        {
            _logger?.LogDebug("Received answer from {PeerId}", fromPeerId);

            if (MessageReceived != null && PeerId.TryParse(fromPeerId, out var peerId))
            {
                await MessageReceived.Invoke(new AnswerMessage
                {
                    FromPeerId = peerId,
                    Sdp = sdp
                });
            }
        });

        _hubConnection.On<string, string, string?, int?>("ReceiveIceCandidate",
            async (fromPeerId, candidate, sdpMid, sdpMLineIndex) =>
            {
                _logger?.LogDebug("Received ICE candidate from {PeerId}", fromPeerId);

                if (MessageReceived != null && PeerId.TryParse(fromPeerId, out var peerId))
                {
                    await MessageReceived.Invoke(new IceCandidateMessage
                    {
                        FromPeerId = peerId,
                        Candidate = candidate,
                        SdpMid = sdpMid,
                        SdpMLineIndex = sdpMLineIndex
                    });
                }
            });

        _hubConnection.On<string, string, byte[]>("PeerJoined",
            async (peerId, displayName, publicKey) =>
            {
                _logger?.LogDebug("Peer joined: {PeerId} ({DisplayName})", peerId, displayName);

                if (MessageReceived != null && PeerId.TryParse(peerId, out var id))
                {
                    await MessageReceived.Invoke(new PeerJoinedMessage
                    {
                        FromPeerId = id,
                        Peer = new PeerInfo
                        {
                            Id = id,
                            DisplayName = displayName,
                            PublicKey = publicKey
                        }
                    });
                }
            });

        _hubConnection.On<string>("PeerLeft", async (peerId) =>
        {
            _logger?.LogDebug("Peer left: {PeerId}", peerId);

            if (MessageReceived != null && PeerId.TryParse(peerId, out var id))
            {
                await MessageReceived.Invoke(new PeerLeftMessage
                {
                    FromPeerId = id
                });
            }
        });

        _hubConnection.Closed += async error =>
        {
            _logger?.LogWarning(error, "Signaling connection closed");
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged.Invoke(false);
        };

        _hubConnection.Reconnected += async _ =>
        {
            _logger?.LogInformation("Signaling connection reconnected");
            await RegisterAsync();
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged.Invoke(true);
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger?.LogInformation("Connecting to signaling server...");
        await _hubConnection.StartAsync(cancellationToken);
        await RegisterAsync();

        _logger?.LogInformation("Connected to signaling server");

        if (ConnectionStateChanged != null)
            await ConnectionStateChanged.Invoke(true);
    }

    private async Task RegisterAsync()
    {
        await _hubConnection.InvokeAsync("Register",
            _localIdentity.Id.ToBase58(),
            _localIdentity.DisplayName,
            _localIdentity.PublicKey);
    }

    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        _logger?.LogInformation("Disconnecting from signaling server...");
        await _hubConnection.StopAsync();

        if (ConnectionStateChanged != null)
            await ConnectionStateChanged.Invoke(false);
    }

    public async Task SendOfferAsync(PeerId targetPeer, string sdp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _hubConnection.InvokeAsync("SendOffer", targetPeer.ToBase58(), sdp);
        _logger?.LogDebug("Sent offer to {PeerId}", targetPeer);
    }

    public async Task SendAnswerAsync(PeerId targetPeer, string sdp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _hubConnection.InvokeAsync("SendAnswer", targetPeer.ToBase58(), sdp);
        _logger?.LogDebug("Sent answer to {PeerId}", targetPeer);
    }

    public async Task SendIceCandidateAsync(PeerId targetPeer, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _hubConnection.InvokeAsync("SendIceCandidate",
            targetPeer.ToBase58(), candidate, sdpMid, sdpMLineIndex);
        _logger?.LogDebug("Sent ICE candidate to {PeerId}", targetPeer);
    }

    public async Task<IReadOnlyList<PeerInfo>> GetOnlinePeersAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var peers = await _hubConnection.InvokeAsync<List<OnlinePeerDto>>("GetOnlinePeers");

        return peers.Select(p => new PeerInfo
        {
            Id = PeerId.Parse(p.PeerId),
            DisplayName = p.DisplayName,
            PublicKey = p.PublicKey
        }).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _hubConnection.DisposeAsync();
    }

    private sealed record OnlinePeerDto(string PeerId, string DisplayName, byte[] PublicKey);
}
