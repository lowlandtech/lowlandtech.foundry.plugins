using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.Collaboration.Presence;

/// <summary>
/// CRDT-based presence service that syncs presence state across peers.
/// Uses LWW semantics for each peer's state.
/// </summary>
public sealed class CrdtPresenceService : IPresenceService, IAsyncDisposable
{
    private readonly PeerIdentity _localIdentity;
    private readonly ILogger<CrdtPresenceService>? _logger;
    private readonly ConcurrentDictionary<string, PresenceState> _peerStates = new();
    private readonly ConcurrentDictionary<string, PeerConnection> _connections = new();
    private readonly Timer _heartbeatTimer;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _offlineThreshold = TimeSpan.FromSeconds(30);
    private readonly string[] _cursorColors =
    [
        "#007bff", "#28a745", "#dc3545", "#ffc107", "#17a2b8",
        "#6f42c1", "#fd7e14", "#20c997", "#e83e8c", "#6c757d"
    ];

    private PresenceState _localState;
    private bool _disposed;

    public PresenceState LocalState => _localState;
    public IReadOnlyDictionary<string, PresenceState> PeerStates => _peerStates;

    public event Action<string, PresenceState>? PresenceChanged;
    public event Action<string>? PeerOffline;
    public event Action<string, PresenceState>? PeerOnline;

    public CrdtPresenceService(
        PeerIdentity localIdentity,
        string displayName,
        ILogger<CrdtPresenceService>? logger = null)
    {
        _localIdentity = localIdentity;
        _logger = logger;

        _localState = new PresenceState
        {
            PeerId = localIdentity.Id.ToBase58(),
            DisplayName = displayName,
            Status = PresenceStatus.Online,
            LastSeen = DateTimeOffset.UtcNow
        };

        // Add local state to peer states
        _peerStates[_localState.PeerId] = _localState;

        // Start heartbeat to broadcast local state
        _heartbeatTimer = new Timer(
            _ => BroadcastLocalState(),
            null,
            _heartbeatInterval,
            _heartbeatInterval);

        // Start cleanup timer to detect offline peers
        _cleanupTimer = new Timer(
            _ => CleanupOfflinePeers(),
            null,
            _offlineThreshold,
            TimeSpan.FromSeconds(10));

        _logger?.LogInformation("Presence service started for {DisplayName}", displayName);
    }

    /// <summary>
    /// Registers a peer connection for presence sync.
    /// </summary>
    public void RegisterConnection(PeerConnection connection)
    {
        var peerId = connection.RemotePeerId.ToBase58();
        _connections[peerId] = connection;

        connection.MessageReceived += async data =>
        {
            try
            {
                await HandleMessageAsync(peerId, data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling presence message from {PeerId}", peerId);
            }
        };

        // Send our current state to the new peer
        _ = SendStateToPeerAsync(connection);
    }

    /// <summary>
    /// Unregisters a peer connection.
    /// </summary>
    public void UnregisterConnection(PeerId peerId)
    {
        var peerIdStr = peerId.ToBase58();
        _connections.TryRemove(peerIdStr, out _);

        if (_peerStates.TryRemove(peerIdStr, out var state))
        {
            state.Status = PresenceStatus.Offline;
            PeerOffline?.Invoke(peerIdStr);
            _logger?.LogInformation("Peer {DisplayName} disconnected", state.DisplayName);
        }
    }

    public void UpdateLocalState(Action<PresenceState> update)
    {
        update(_localState);
        _localState.LastSeen = DateTimeOffset.UtcNow;
        BroadcastLocalState();
    }

    public void SetStatus(PresenceStatus status, string? statusMessage = null)
    {
        _localState.Status = status;
        _localState.StatusMessage = statusMessage;
        _localState.LastSeen = DateTimeOffset.UtcNow;
        BroadcastLocalState();
    }

    public void SetActivity(PresenceActivity activity)
    {
        _localState.Activity = activity;
        _localState.LastSeen = DateTimeOffset.UtcNow;
        BroadcastLocalState();
    }

    public void SetCursor(Guid documentId, int position, int? selectionEnd = null)
    {
        _localState.Cursor = new CursorPosition
        {
            DocumentId = documentId,
            Position = position,
            SelectionEnd = selectionEnd,
            Color = GetCursorColor(_localState.PeerId)
        };
        _localState.LastSeen = DateTimeOffset.UtcNow;
        BroadcastLocalState();
    }

    public void ClearCursor()
    {
        _localState.Cursor = null;
        _localState.LastSeen = DateTimeOffset.UtcNow;
        BroadcastLocalState();
    }

    private string GetCursorColor(string peerId)
    {
        var hash = peerId.GetHashCode();
        return _cursorColors[Math.Abs(hash) % _cursorColors.Length];
    }

    private void BroadcastLocalState()
    {
        if (_disposed) return;

        var envelope = new PresenceEnvelope
        {
            Type = PresenceMessageType.StateUpdate,
            Payload = MessagePackSerializer.Serialize(SerializeState(_localState))
        };

        var data = MessagePackSerializer.Serialize(envelope);

        foreach (var (_, connection) in _connections)
        {
            _ = connection.SendAsync(data);
        }
    }

    private async Task SendStateToPeerAsync(PeerConnection connection)
    {
        var envelope = new PresenceEnvelope
        {
            Type = PresenceMessageType.StateUpdate,
            Payload = MessagePackSerializer.Serialize(SerializeState(_localState))
        };

        var data = MessagePackSerializer.Serialize(envelope);
        await connection.SendAsync(data);
    }

    private async Task HandleMessageAsync(string fromPeerId, ReadOnlyMemory<byte> data)
    {
        var envelope = MessagePackSerializer.Deserialize<PresenceEnvelope>(data);

        switch (envelope.Type)
        {
            case PresenceMessageType.StateUpdate:
                await HandleStateUpdateAsync(fromPeerId, envelope.Payload);
                break;

            case PresenceMessageType.Ping:
                await HandlePingAsync(fromPeerId);
                break;
        }
    }

    private Task HandleStateUpdateAsync(string fromPeerId, byte[] payload)
    {
        var stateData = MessagePackSerializer.Deserialize<PresenceStateData>(payload);
        var state = DeserializeState(stateData);

        var isNew = !_peerStates.ContainsKey(fromPeerId);
        state.Cursor ??= new CursorPosition { Color = GetCursorColor(fromPeerId) };
        if (state.Cursor != null)
        {
            state.Cursor.Color = GetCursorColor(fromPeerId);
        }

        _peerStates[fromPeerId] = state;

        if (isNew)
        {
            PeerOnline?.Invoke(fromPeerId, state);
            _logger?.LogInformation("Peer {DisplayName} came online", state.DisplayName);
        }

        PresenceChanged?.Invoke(fromPeerId, state);

        return Task.CompletedTask;
    }

    private async Task HandlePingAsync(string fromPeerId)
    {
        // Respond with current state
        if (_connections.TryGetValue(fromPeerId, out var connection))
        {
            await SendStateToPeerAsync(connection);
        }
    }

    private void CleanupOfflinePeers()
    {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;
        var offlinePeers = new List<string>();

        foreach (var (peerId, state) in _peerStates)
        {
            if (peerId == _localState.PeerId)
                continue;

            if (now - state.LastSeen > _offlineThreshold)
            {
                offlinePeers.Add(peerId);
            }
        }

        foreach (var peerId in offlinePeers)
        {
            if (_peerStates.TryRemove(peerId, out var state))
            {
                _connections.TryRemove(peerId, out _);
                PeerOffline?.Invoke(peerId);
                _logger?.LogInformation("Peer {DisplayName} went offline (timeout)", state.DisplayName);
            }
        }
    }

    private static PresenceStateData SerializeState(PresenceState state)
    {
        return new PresenceStateData
        {
            PeerId = state.PeerId,
            DisplayName = state.DisplayName,
            AvatarUrl = state.AvatarUrl,
            Status = (int)state.Status,
            StatusMessage = state.StatusMessage,
            ActivityType = (int)state.Activity.Type,
            ActivityResourceId = state.Activity.ResourceId,
            ActivityDescription = state.Activity.Description,
            CursorDocumentId = state.Cursor?.DocumentId,
            CursorPosition = state.Cursor?.Position,
            CursorSelectionEnd = state.Cursor?.SelectionEnd,
            LastSeen = state.LastSeen.ToUnixTimeMilliseconds(),
            CustomData = state.CustomData
        };
    }

    private static PresenceState DeserializeState(PresenceStateData data)
    {
        var state = new PresenceState
        {
            PeerId = data.PeerId,
            DisplayName = data.DisplayName,
            AvatarUrl = data.AvatarUrl,
            Status = (PresenceStatus)data.Status,
            StatusMessage = data.StatusMessage,
            Activity = new PresenceActivity
            {
                Type = (ActivityType)data.ActivityType,
                ResourceId = data.ActivityResourceId,
                Description = data.ActivityDescription
            },
            LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(data.LastSeen),
            CustomData = data.CustomData ?? new Dictionary<string, string>()
        };

        if (data.CursorDocumentId.HasValue)
        {
            state.Cursor = new CursorPosition
            {
                DocumentId = data.CursorDocumentId.Value,
                Position = data.CursorPosition ?? 0,
                SelectionEnd = data.CursorSelectionEnd
            };
        }

        return state;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _heartbeatTimer.DisposeAsync();
        await _cleanupTimer.DisposeAsync();

        _connections.Clear();
        _peerStates.Clear();

        _logger?.LogInformation("Presence service disposed");
    }
}

internal enum PresenceMessageType
{
    StateUpdate = 0,
    Ping = 1,
    Pong = 2
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class PresenceEnvelope
{
    [Key(0)]
    public PresenceMessageType Type { get; set; }

    [Key(1)]
    public byte[] Payload { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class PresenceStateData
{
    [Key(0)]
    public string PeerId { get; set; } = "";

    [Key(1)]
    public string DisplayName { get; set; } = "";

    [Key(2)]
    public string? AvatarUrl { get; set; }

    [Key(3)]
    public int Status { get; set; }

    [Key(4)]
    public string? StatusMessage { get; set; }

    [Key(5)]
    public int ActivityType { get; set; }

    [Key(6)]
    public Guid? ActivityResourceId { get; set; }

    [Key(7)]
    public string? ActivityDescription { get; set; }

    [Key(8)]
    public Guid? CursorDocumentId { get; set; }

    [Key(9)]
    public int? CursorPosition { get; set; }

    [Key(10)]
    public int? CursorSelectionEnd { get; set; }

    [Key(11)]
    public long LastSeen { get; set; }

    [Key(12)]
    public Dictionary<string, string>? CustomData { get; set; }
}
