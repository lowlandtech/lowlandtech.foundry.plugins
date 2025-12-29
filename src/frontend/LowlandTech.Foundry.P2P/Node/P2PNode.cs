using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Node;

/// <summary>
/// Default implementation of IP2PNode.
/// </summary>
public sealed class P2PNode : IP2PNode
{
    private readonly IDataChannelFactory _channelFactory;
    private readonly ILogger<P2PNode>? _logger;
    private readonly ConcurrentDictionary<PeerId, PeerConnection> _connections = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public PeerIdentity Identity { get; }
    public NodeMode Mode { get; private set; }
    public bool IsRunning { get; private set; }
    public IReadOnlyDictionary<PeerId, PeerConnection> ConnectedPeers => _connections;

    public event Func<PeerConnection, Task>? PeerConnected;
    public event Func<PeerId, DisconnectReason, Task>? PeerDisconnected;
    public event Func<PeerId, ReadOnlyMemory<byte>, Task>? MessageReceived;

    public P2PNode(
        PeerIdentity identity,
        IDataChannelFactory channelFactory,
        ILogger<P2PNode>? logger = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _logger = logger;
    }

    public async Task StartAsync(NodeMode mode, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
                throw new InvalidOperationException("Node is already running");

            Mode = mode;
            IsRunning = true;

            _logger?.LogInformation("P2P node started in {Mode} mode. PeerId: {PeerId}",
                mode, Identity.Id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!IsRunning) return;

            _logger?.LogInformation("Stopping P2P node...");

            // Disconnect all peers
            var disconnectTasks = _connections.Keys
                .Select(peerId => DisconnectInternalAsync(peerId, DisconnectReason.Shutdown))
                .ToArray();

            await Task.WhenAll(disconnectTasks);

            IsRunning = false;
            _logger?.LogInformation("P2P node stopped");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SwitchModeAsync(NodeMode newMode)
    {
        await _lock.WaitAsync();
        try
        {
            if (!IsRunning)
                throw new InvalidOperationException("Node is not running");

            if (Mode == newMode) return;

            var oldMode = Mode;
            Mode = newMode;

            _logger?.LogInformation("Switched node mode from {OldMode} to {NewMode}", oldMode, newMode);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PeerConnection> ConnectAsync(PeerInfo peer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
            throw new InvalidOperationException("Node is not running");

        if (_connections.ContainsKey(peer.Id))
            return _connections[peer.Id];

        _logger?.LogDebug("Connecting to peer {PeerId} ({DisplayName})...", peer.Id, peer.DisplayName);

        try
        {
            var channel = await _channelFactory.CreateChannelAsync(peer, cancellationToken);
            var connection = new PeerConnection(peer, channel);

            connection.MessageReceived += async message =>
            {
                if (MessageReceived != null)
                    await MessageReceived.Invoke(peer.Id, message);
            };

            connection.StateChanged += async state =>
            {
                if (state == ConnectionState.Disconnected || state == ConnectionState.Failed)
                {
                    await HandleDisconnectionAsync(peer.Id,
                        state == ConnectionState.Failed ? DisconnectReason.NetworkError : DisconnectReason.RemoteDisconnect);
                }
            };

            if (!_connections.TryAdd(peer.Id, connection))
            {
                await connection.DisposeAsync();
                return _connections[peer.Id];
            }

            _logger?.LogInformation("Connected to peer {PeerId} ({DisplayName})", peer.Id, peer.DisplayName);

            if (PeerConnected != null)
                await PeerConnected.Invoke(connection);

            return connection;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to peer {PeerId}", peer.Id);
            throw;
        }
    }

    public Task DisconnectAsync(PeerId peerId)
    {
        return DisconnectInternalAsync(peerId, DisconnectReason.LocalDisconnect);
    }

    private async Task DisconnectInternalAsync(PeerId peerId, DisconnectReason reason)
    {
        if (_connections.TryRemove(peerId, out var connection))
        {
            _logger?.LogInformation("Disconnecting from peer {PeerId}. Reason: {Reason}", peerId, reason);

            await connection.DisposeAsync();

            if (PeerDisconnected != null)
                await PeerDisconnected.Invoke(peerId, reason);
        }
    }

    private async Task HandleDisconnectionAsync(PeerId peerId, DisconnectReason reason)
    {
        if (_connections.TryRemove(peerId, out var connection))
        {
            _logger?.LogInformation("Peer {PeerId} disconnected. Reason: {Reason}", peerId, reason);

            await connection.DisposeAsync();

            if (PeerDisconnected != null)
                await PeerDisconnected.Invoke(peerId, reason);
        }
    }

    public async Task SendAsync(PeerId peerId, ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_connections.TryGetValue(peerId, out var connection))
            throw new InvalidOperationException($"Not connected to peer {peerId}");

        await connection.SendAsync(message, cancellationToken);
    }

    public async Task BroadcastAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sendTasks = _connections.Values
            .Where(c => c.State == ConnectionState.Connected)
            .Select(c => c.SendAsync(message, cancellationToken))
            .ToArray();

        await Task.WhenAll(sendTasks);
    }

    /// <summary>
    /// Adds an incoming connection (called when a remote peer connects to us).
    /// </summary>
    internal async Task<PeerConnection> AcceptConnectionAsync(PeerInfo remotePeer, IDataChannel channel)
    {
        var connection = new PeerConnection(remotePeer, channel);

        connection.MessageReceived += async message =>
        {
            if (MessageReceived != null)
                await MessageReceived.Invoke(remotePeer.Id, message);
        };

        connection.StateChanged += async state =>
        {
            if (state == ConnectionState.Disconnected || state == ConnectionState.Failed)
            {
                await HandleDisconnectionAsync(remotePeer.Id,
                    state == ConnectionState.Failed ? DisconnectReason.NetworkError : DisconnectReason.RemoteDisconnect);
            }
        };

        if (!_connections.TryAdd(remotePeer.Id, connection))
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException($"Already connected to peer {remotePeer.Id}");
        }

        _logger?.LogInformation("Accepted connection from peer {PeerId} ({DisplayName})",
            remotePeer.Id, remotePeer.DisplayName);

        if (PeerConnected != null)
            await PeerConnected.Invoke(connection);

        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
        _lock.Dispose();
        Identity.Dispose();
    }
}
