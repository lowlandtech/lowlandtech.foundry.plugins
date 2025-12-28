using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Core.Transport;
using LowlandTech.Foundry.P2P.Crdt.Storage;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Crdt.Sync;

/// <summary>
/// Delta-based sync protocol - only sends changes since the last known state.
///
/// Protocol flow:
/// 1. Peer A sends StateVectorRequest to Peer B
/// 2. Peer B responds with StateVectorResponse
/// 3. Peer A computes delta since B's state vector and sends Delta
/// 4. Peer B applies delta and sends Ack with new state vector
/// 5. Repeat in reverse direction for bidirectional sync
/// </summary>
public sealed class DeltaSyncProtocol : ISyncProtocol
{
    private readonly ICrdtStore _store;
    private readonly PeerIdentity _localIdentity;
    private readonly ILogger<DeltaSyncProtocol>? _logger;
    private readonly ConcurrentDictionary<PeerId, PeerConnection> _activeConnections = new();
    private readonly ConcurrentDictionary<PeerId, Dictionary<Guid, byte[]>> _peerStateVectors = new();

    public event Func<SyncMessage, Task>? MessageReceived;
    public event Func<PeerId, Guid, Task>? SyncCompleted;

    public DeltaSyncProtocol(
        ICrdtStore store,
        PeerIdentity localIdentity,
        ILogger<DeltaSyncProtocol>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _localIdentity = localIdentity ?? throw new ArgumentNullException(nameof(localIdentity));
        _logger = logger;
    }

    public async Task StartSyncAsync(PeerConnection connection, CancellationToken cancellationToken = default)
    {
        var peerId = connection.RemotePeerId;

        if (!_activeConnections.TryAdd(peerId, connection))
        {
            _logger?.LogWarning("Sync already active with peer {PeerId}", peerId);
            return;
        }

        _logger?.LogInformation("Starting sync with peer {PeerId}", peerId);

        // Set up message handler
        connection.MessageReceived += async data =>
        {
            try
            {
                await HandleMessageAsync(peerId, data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling sync message from {PeerId}", peerId);
            }
        };

        // Initialize state vector tracking
        _peerStateVectors[peerId] = new Dictionary<Guid, byte[]>();

        await Task.CompletedTask;
    }

    public async Task StopSyncAsync(PeerId peerId)
    {
        if (_activeConnections.TryRemove(peerId, out _))
        {
            _peerStateVectors.TryRemove(peerId, out _);
            _logger?.LogInformation("Stopped sync with peer {PeerId}", peerId);
        }

        await Task.CompletedTask;
    }

    public async Task RequestSyncAsync(PeerId peerId, Guid documentId)
    {
        if (!_activeConnections.TryGetValue(peerId, out var connection))
        {
            throw new InvalidOperationException($"No active sync with peer {peerId}");
        }

        _logger?.LogDebug("Requesting sync for document {DocumentId} from {PeerId}", documentId, peerId);

        var request = new StateVectorRequest
        {
            MessageId = Guid.NewGuid(),
            SenderId = _localIdentity.Id,
            Timestamp = DateTimeOffset.UtcNow,
            DocumentId = documentId
        };

        await SendMessageAsync(connection, request);
    }

    private async Task HandleMessageAsync(PeerId fromPeerId, ReadOnlyMemory<byte> data)
    {
        var envelope = MessagePackSerializer.Deserialize<SyncEnvelope>(data);

        switch (envelope.Type)
        {
            case SyncMessageType.StateVectorRequest:
                await HandleStateVectorRequestAsync(fromPeerId, envelope.Payload);
                break;

            case SyncMessageType.StateVectorResponse:
                await HandleStateVectorResponseAsync(fromPeerId, envelope.Payload);
                break;

            case SyncMessageType.Delta:
                await HandleDeltaAsync(fromPeerId, envelope.Payload);
                break;

            case SyncMessageType.Ack:
                await HandleAckAsync(fromPeerId, envelope.Payload);
                break;

            case SyncMessageType.FullSyncRequest:
                await HandleFullSyncRequestAsync(fromPeerId, envelope.Payload);
                break;
        }
    }

    private async Task HandleStateVectorRequestAsync(PeerId fromPeerId, byte[] payload)
    {
        var request = MessagePackSerializer.Deserialize<StateVectorRequestPayload>(payload);

        _logger?.LogDebug("Received state vector request for {DocumentId} from {PeerId}",
            request.DocumentId, fromPeerId);

        // Get local CRDT and its state vector
        var crdt = await _store.GetAsync(request.DocumentId);
        var stateVector = crdt?.GetStateVector() ?? [];

        if (!_activeConnections.TryGetValue(fromPeerId, out var connection))
            return;

        var response = new StateVectorResponse
        {
            MessageId = Guid.NewGuid(),
            SenderId = _localIdentity.Id,
            Timestamp = DateTimeOffset.UtcNow,
            DocumentId = request.DocumentId,
            StateVector = stateVector
        };

        await SendMessageAsync(connection, response);
    }

    private async Task HandleStateVectorResponseAsync(PeerId fromPeerId, byte[] payload)
    {
        var response = MessagePackSerializer.Deserialize<StateVectorResponsePayload>(payload);

        _logger?.LogDebug("Received state vector response for {DocumentId} from {PeerId}",
            response.DocumentId, fromPeerId);

        // Store peer's state vector
        if (_peerStateVectors.TryGetValue(fromPeerId, out var vectors))
        {
            vectors[response.DocumentId] = response.StateVector;
        }

        // Get local CRDT and compute delta
        var crdt = await _store.GetAsync(response.DocumentId);
        if (crdt == null) return;

        var delta = crdt.EncodeDelta(response.StateVector);

        if (delta.Length > 0 && _activeConnections.TryGetValue(fromPeerId, out var connection))
        {
            var deltaMessage = new DeltaMessage
            {
                MessageId = Guid.NewGuid(),
                SenderId = _localIdentity.Id,
                Timestamp = DateTimeOffset.UtcNow,
                DocumentId = response.DocumentId,
                Delta = delta,
                NewStateVector = crdt.GetStateVector()
            };

            await SendMessageAsync(connection, deltaMessage);
        }
    }

    private async Task HandleDeltaAsync(PeerId fromPeerId, byte[] payload)
    {
        var delta = MessagePackSerializer.Deserialize<DeltaPayload>(payload);

        _logger?.LogDebug("Received delta for {DocumentId} from {PeerId}", delta.DocumentId, fromPeerId);

        // Apply delta to local CRDT
        var crdt = await _store.GetAsync(delta.DocumentId);
        crdt?.ApplyDelta(delta.Delta);

        if (crdt != null)
        {
            await _store.SaveAsync(crdt);
        }

        // Send acknowledgment
        if (_activeConnections.TryGetValue(fromPeerId, out var connection))
        {
            var ack = new AckMessage
            {
                MessageId = Guid.NewGuid(),
                SenderId = _localIdentity.Id,
                Timestamp = DateTimeOffset.UtcNow,
                DocumentId = delta.DocumentId,
                StateVector = crdt?.GetStateVector() ?? []
            };

            await SendMessageAsync(connection, ack);
        }

        // Notify completion
        if (SyncCompleted != null)
        {
            await SyncCompleted.Invoke(fromPeerId, delta.DocumentId);
        }
    }

    private async Task HandleAckAsync(PeerId fromPeerId, byte[] payload)
    {
        var ack = MessagePackSerializer.Deserialize<AckPayload>(payload);

        _logger?.LogDebug("Received ack for {DocumentId} from {PeerId}", ack.DocumentId, fromPeerId);

        // Update peer's known state vector
        if (_peerStateVectors.TryGetValue(fromPeerId, out var vectors))
        {
            vectors[ack.DocumentId] = ack.StateVector;
        }

        // Notify completion
        if (SyncCompleted != null)
        {
            await SyncCompleted.Invoke(fromPeerId, ack.DocumentId);
        }
    }

    private async Task HandleFullSyncRequestAsync(PeerId fromPeerId, byte[] payload)
    {
        var request = MessagePackSerializer.Deserialize<FullSyncRequestPayload>(payload);

        _logger?.LogDebug("Received full sync request for {DocumentId} from {PeerId}",
            request.DocumentId, fromPeerId);

        // Send full state as delta
        var crdt = await _store.GetAsync(request.DocumentId);
        if (crdt == null || !_activeConnections.TryGetValue(fromPeerId, out var connection))
            return;

        var delta = crdt.EncodeDelta([]);

        var deltaMessage = new DeltaMessage
        {
            MessageId = Guid.NewGuid(),
            SenderId = _localIdentity.Id,
            Timestamp = DateTimeOffset.UtcNow,
            DocumentId = request.DocumentId,
            Delta = delta,
            NewStateVector = crdt.GetStateVector()
        };

        await SendMessageAsync(connection, deltaMessage);
    }

    private async Task SendMessageAsync(PeerConnection connection, SyncMessage message)
    {
        byte[] payload = message switch
        {
            StateVectorRequest r => MessagePackSerializer.Serialize(new StateVectorRequestPayload { DocumentId = r.DocumentId }),
            StateVectorResponse r => MessagePackSerializer.Serialize(new StateVectorResponsePayload { DocumentId = r.DocumentId, StateVector = r.StateVector }),
            DeltaMessage d => MessagePackSerializer.Serialize(new DeltaPayload { DocumentId = d.DocumentId, Delta = d.Delta, NewStateVector = d.NewStateVector }),
            AckMessage a => MessagePackSerializer.Serialize(new AckPayload { DocumentId = a.DocumentId, StateVector = a.StateVector }),
            FullSyncRequest f => MessagePackSerializer.Serialize(new FullSyncRequestPayload { DocumentId = f.DocumentId }),
            _ => throw new ArgumentException($"Unknown message type: {message.GetType()}")
        };

        var envelope = new SyncEnvelope
        {
            Type = message.Type,
            Payload = payload
        };

        var data = MessagePackSerializer.Serialize(envelope);
        await connection.SendAsync(data);

        if (MessageReceived != null)
        {
            await MessageReceived.Invoke(message);
        }
    }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class SyncEnvelope
{
    [Key(0)]
    public SyncMessageType Type { get; set; }

    [Key(1)]
    public byte[] Payload { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class StateVectorRequestPayload
{
    [Key(0)]
    public Guid DocumentId { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class StateVectorResponsePayload
{
    [Key(0)]
    public Guid DocumentId { get; set; }

    [Key(1)]
    public byte[] StateVector { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class DeltaPayload
{
    [Key(0)]
    public Guid DocumentId { get; set; }

    [Key(1)]
    public byte[] Delta { get; set; } = [];

    [Key(2)]
    public byte[] NewStateVector { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AckPayload
{
    [Key(0)]
    public Guid DocumentId { get; set; }

    [Key(1)]
    public byte[] StateVector { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class FullSyncRequestPayload
{
    [Key(0)]
    public Guid DocumentId { get; set; }
}
