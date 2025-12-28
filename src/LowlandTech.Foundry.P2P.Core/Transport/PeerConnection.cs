using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Core.Transport;

/// <summary>
/// Represents an active connection to a remote peer.
/// </summary>
public sealed class PeerConnection : IAsyncDisposable
{
    private readonly IDataChannel _dataChannel;
    private bool _disposed;

    /// <summary>
    /// The remote peer's identity.
    /// </summary>
    public PeerInfo RemotePeer { get; }

    /// <summary>
    /// The remote peer's ID.
    /// </summary>
    public PeerId RemotePeerId => RemotePeer.Id;

    /// <summary>
    /// Current state of the connection.
    /// </summary>
    public ConnectionState State { get; private set; }

    /// <summary>
    /// When the connection was established.
    /// </summary>
    public DateTimeOffset ConnectedAt { get; }

    /// <summary>
    /// Last time data was received from this peer.
    /// </summary>
    public DateTimeOffset LastActivity { get; private set; }

    /// <summary>
    /// Round-trip latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; internal set; }

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    public event Func<ConnectionState, Task>? StateChanged;

    /// <summary>
    /// Raised when a message is received.
    /// </summary>
    public event Func<ReadOnlyMemory<byte>, Task>? MessageReceived;

    internal PeerConnection(PeerInfo remotePeer, IDataChannel dataChannel)
    {
        RemotePeer = remotePeer;
        _dataChannel = dataChannel;
        State = ConnectionState.Connected;
        ConnectedAt = DateTimeOffset.UtcNow;
        LastActivity = DateTimeOffset.UtcNow;

        _dataChannel.MessageReceived += OnMessageReceived;
        _dataChannel.StateChanged += OnStateChanged;
    }

    /// <summary>
    /// Sends a message to the remote peer.
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (State != ConnectionState.Connected)
            throw new InvalidOperationException($"Cannot send: connection is {State}");

        await _dataChannel.SendAsync(data, cancellationToken);
    }

    private async Task OnMessageReceived(ReadOnlyMemory<byte> message)
    {
        LastActivity = DateTimeOffset.UtcNow;

        if (MessageReceived != null)
            await MessageReceived.Invoke(message);
    }

    private async Task OnStateChanged(DataChannelState state)
    {
        State = state switch
        {
            DataChannelState.Open => ConnectionState.Connected,
            DataChannelState.Connecting => ConnectionState.Connecting,
            DataChannelState.Closing => ConnectionState.Disconnecting,
            DataChannelState.Closed => ConnectionState.Disconnected,
            DataChannelState.Failed => ConnectionState.Failed,
            _ => State
        };

        if (StateChanged != null)
            await StateChanged.Invoke(State);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _dataChannel.MessageReceived -= OnMessageReceived;
        _dataChannel.StateChanged -= OnStateChanged;

        await _dataChannel.DisposeAsync();
        State = ConnectionState.Disconnected;
    }
}

/// <summary>
/// Connection state.
/// </summary>
public enum ConnectionState
{
    /// <summary>Connection is being established.</summary>
    Connecting,

    /// <summary>Connection is open and ready.</summary>
    Connected,

    /// <summary>Connection is being closed.</summary>
    Disconnecting,

    /// <summary>Connection has been closed.</summary>
    Disconnected,

    /// <summary>Connection failed.</summary>
    Failed
}
