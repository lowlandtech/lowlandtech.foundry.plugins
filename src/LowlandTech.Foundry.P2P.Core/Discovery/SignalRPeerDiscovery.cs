using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Core.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Core.Discovery;

/// <summary>
/// SignalR-based peer discovery for WAN (internet) peers.
/// Connects to a signaling server to discover remote peers.
/// </summary>
public sealed class SignalRPeerDiscovery : IPeerDiscovery
{
    private readonly string _signalingServerUrl;
    private readonly ILogger<SignalRPeerDiscovery>? _logger;
    private readonly ConcurrentDictionary<PeerId, PeerInfo> _knownPeers = new();
    private HubConnection? _hubConnection;
    private PeerInfo? _localPeer;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event Func<PeerInfo, Task>? PeerDiscovered;
    public event Func<PeerId, Task>? PeerLost;

    public SignalRPeerDiscovery(
        string signalingServerUrl,
        ILogger<SignalRPeerDiscovery>? logger = null)
    {
        _signalingServerUrl = signalingServerUrl ?? throw new ArgumentNullException(nameof(signalingServerUrl));
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;

        _logger?.LogInformation("Starting SignalR peer discovery...");

        var signalingEndpoint = _signalingServerUrl.TrimEnd('/') + "/signaling";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(signalingEndpoint)
            .WithAutomaticReconnect()
            .Build();

        SetupHandlers();

        await _hubConnection.StartAsync(cancellationToken);

        // Fetch existing online peers
        await RefreshPeerListAsync();

        IsRunning = true;

        _logger?.LogInformation("SignalR peer discovery started");
    }

    private void SetupHandlers()
    {
        if (_hubConnection == null) return;

        _hubConnection.On<string, string, byte[]>("PeerJoined",
            async (peerId, displayName, publicKey) =>
            {
                await HandlePeerJoinedAsync(peerId, displayName, publicKey);
            });

        _hubConnection.On<string>("PeerLeft", async (peerId) =>
        {
            await HandlePeerLeftAsync(peerId);
        });

        _hubConnection.Closed += async error =>
        {
            _logger?.LogWarning(error, "SignalR connection closed");
            IsRunning = false;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger?.LogInformation("SignalR connection reconnected");
            IsRunning = true;

            // Re-announce and refresh peer list
            if (_localPeer != null)
            {
                await RegisterAsync(_localPeer);
            }
            await RefreshPeerListAsync();
        };
    }

    private async Task HandlePeerJoinedAsync(string peerIdStr, string displayName, byte[] publicKey)
    {
        if (!PeerId.TryParse(peerIdStr, out var peerId))
        {
            _logger?.LogWarning("Invalid peerId received: {PeerId}", peerIdStr);
            return;
        }

        // Ignore our own announcements
        if (_localPeer != null && peerId == _localPeer.Id)
            return;

        var peerInfo = new PeerInfo
        {
            Id = peerId,
            PublicKey = publicKey,
            DisplayName = displayName,
            Endpoints = [$"signalr:{peerIdStr}"], // Use signalr: prefix for WAN peers
            LastSeen = DateTimeOffset.UtcNow
        };

        if (_knownPeers.TryAdd(peerId, peerInfo))
        {
            _logger?.LogInformation("WAN peer discovered: {PeerId} ({DisplayName})", peerId, displayName);

            if (PeerDiscovered != null)
                await PeerDiscovered.Invoke(peerInfo);
        }
        else
        {
            _knownPeers[peerId] = peerInfo;
        }
    }

    private async Task HandlePeerLeftAsync(string peerIdStr)
    {
        if (PeerId.TryParse(peerIdStr, out var peerId))
        {
            if (_knownPeers.TryRemove(peerId, out var peerInfo))
            {
                _logger?.LogInformation("WAN peer left: {PeerId} ({DisplayName})", peerId, peerInfo.DisplayName);

                if (PeerLost != null)
                    await PeerLost.Invoke(peerId);
            }
        }
    }

    private async Task RefreshPeerListAsync()
    {
        if (_hubConnection == null) return;

        try
        {
            var peers = await _hubConnection.InvokeAsync<List<OnlinePeerDto>>("GetOnlinePeers");

            foreach (var peer in peers)
            {
                if (!PeerId.TryParse(peer.PeerId, out var peerId))
                    continue;

                // Ignore our own entry
                if (_localPeer != null && peerId == _localPeer.Id)
                    continue;

                var peerInfo = new PeerInfo
                {
                    Id = peerId,
                    PublicKey = peer.PublicKey,
                    DisplayName = peer.DisplayName,
                    Endpoints = [$"signalr:{peer.PeerId}"],
                    LastSeen = DateTimeOffset.UtcNow
                };

                if (_knownPeers.TryAdd(peerId, peerInfo))
                {
                    _logger?.LogDebug("Existing WAN peer: {PeerId} ({DisplayName})", peerId, peer.DisplayName);

                    if (PeerDiscovered != null)
                        await PeerDiscovered.Invoke(peerInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh peer list");
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _logger?.LogInformation("Stopping SignalR peer discovery...");

        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _knownPeers.Clear();
        IsRunning = false;

        _logger?.LogInformation("SignalR peer discovery stopped");
    }

    public Task<IReadOnlyList<PeerInfo>> GetKnownPeersAsync()
    {
        var peers = _knownPeers.Values.ToList();
        return Task.FromResult<IReadOnlyList<PeerInfo>>(peers);
    }

    public async Task AnnounceAsync(PeerInfo localPeer)
    {
        _localPeer = localPeer;

        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await RegisterAsync(localPeer);
        }
    }

    private async Task RegisterAsync(PeerInfo localPeer)
    {
        if (_hubConnection == null) return;

        await _hubConnection.InvokeAsync("Register",
            localPeer.Id.ToBase58(),
            localPeer.DisplayName,
            localPeer.PublicKey);

        _logger?.LogInformation("Announced local peer on SignalR: {DisplayName}", localPeer.DisplayName);
    }

    public async Task UnannounceAsync()
    {
        // Disconnecting from the hub will automatically remove the peer
        _localPeer = null;
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
    }

    private sealed record OnlinePeerDto(string PeerId, string DisplayName, byte[] PublicKey);
}
