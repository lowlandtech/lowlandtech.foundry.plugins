using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;

namespace LowlandTech.Foundry.Collaboration.Chat;

/// <summary>
/// A CRDT-backed chat room supporting real-time messaging between peers.
/// Uses an LWW-Element-Set approach for message ordering and conflict resolution.
/// </summary>
public sealed class ChatRoom : CrdtBase<IReadOnlyList<ChatMessage>>
{
    private readonly SortedDictionary<HybridLogicalClock, ChatMessage> _messages = new();
    private readonly Dictionary<Guid, ChatMessage> _messageIndex = new();
    private readonly HashSet<string> _participants = new();
    private HybridLogicalClock _lastTimestamp;

    /// <summary>
    /// Gets the room name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the room description.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the participants in this room.
    /// </summary>
    public IReadOnlySet<string> Participants => _participants;

    /// <summary>
    /// Gets all messages in chronological order.
    /// </summary>
    public override IReadOnlyList<ChatMessage> Value =>
        _messages.Values.Where(m => !m.IsDeleted).ToList();

    /// <summary>
    /// Gets the count of visible messages.
    /// </summary>
    public int MessageCount => _messages.Values.Count(m => !m.IsDeleted);

    /// <summary>
    /// Raised when a new message is received.
    /// </summary>
    public event Action<ChatMessage>? MessageReceived;

    /// <summary>
    /// Raised when a message is deleted.
    /// </summary>
    public event Action<Guid>? MessageDeleted;

    /// <summary>
    /// Raised when a message is edited.
    /// </summary>
    public event Action<ChatMessage>? MessageEdited;

    public ChatRoom(Guid id, PeerId localPeerId, string name, string description = "")
        : base(id, localPeerId)
    {
        Name = name;
        Description = description;
        _lastTimestamp = HybridLogicalClock.Now(localPeerId);
        _participants.Add(localPeerId.ToBase58());
    }

    /// <summary>
    /// Sends a new message to the room.
    /// </summary>
    public ChatMessage SendMessage(string content, string senderName, Guid? replyToId = null)
    {
        _lastTimestamp = _lastTimestamp.Tick(LocalPeerId);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RoomId = Id,
            SenderId = LocalPeerId.ToBase58(),
            SenderName = senderName,
            Content = content,
            Timestamp = _lastTimestamp,
            ReplyToId = replyToId,
            Type = ChatMessageType.Text
        };

        AddMessage(message);
        IncrementClock();
        NotifyValueChanged();
        MessageReceived?.Invoke(message);

        return message;
    }

    /// <summary>
    /// Sends a system message (e.g., "User joined").
    /// </summary>
    public ChatMessage SendSystemMessage(string content)
    {
        _lastTimestamp = _lastTimestamp.Tick(LocalPeerId);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RoomId = Id,
            SenderId = LocalPeerId.ToBase58(),
            SenderName = "System",
            Content = content,
            Timestamp = _lastTimestamp,
            Type = ChatMessageType.System
        };

        AddMessage(message);
        IncrementClock();
        NotifyValueChanged();

        return message;
    }

    /// <summary>
    /// Edits an existing message.
    /// </summary>
    public bool EditMessage(Guid messageId, string newContent)
    {
        if (!_messageIndex.TryGetValue(messageId, out var message))
            return false;

        if (message.SenderId != LocalPeerId.ToBase58())
            return false; // Can only edit own messages

        _lastTimestamp = _lastTimestamp.Tick(LocalPeerId);
        message.Content = newContent;
        message.EditedAt = _lastTimestamp;

        IncrementClock();
        NotifyValueChanged();
        MessageEdited?.Invoke(message);

        return true;
    }

    /// <summary>
    /// Deletes a message (marks as tombstone).
    /// </summary>
    public bool DeleteMessage(Guid messageId)
    {
        if (!_messageIndex.TryGetValue(messageId, out var message))
            return false;

        if (message.SenderId != LocalPeerId.ToBase58())
            return false; // Can only delete own messages

        message.IsDeleted = true;
        IncrementClock();
        NotifyValueChanged();
        MessageDeleted?.Invoke(messageId);

        return true;
    }

    /// <summary>
    /// Gets a message by ID.
    /// </summary>
    public ChatMessage? GetMessage(Guid messageId)
    {
        return _messageIndex.TryGetValue(messageId, out var message) ? message : null;
    }

    /// <summary>
    /// Gets messages after a specific timestamp.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetMessagesSince(HybridLogicalClock since)
    {
        return _messages
            .Where(kvp => kvp.Key > since && !kvp.Value.IsDeleted)
            .Select(kvp => kvp.Value)
            .ToList();
    }

    /// <summary>
    /// Gets the most recent messages.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetRecentMessages(int count)
    {
        return _messages.Values
            .Where(m => !m.IsDeleted)
            .TakeLast(count)
            .ToList();
    }

    /// <summary>
    /// Adds a participant to the room.
    /// </summary>
    public void AddParticipant(string peerId)
    {
        if (_participants.Add(peerId))
        {
            IncrementClock();
        }
    }

    /// <summary>
    /// Removes a participant from the room.
    /// </summary>
    public void RemoveParticipant(string peerId)
    {
        if (_participants.Remove(peerId))
        {
            IncrementClock();
        }
    }

    private void AddMessage(ChatMessage message)
    {
        if (_messageIndex.ContainsKey(message.Id))
            return;

        _messages[message.Timestamp] = message;
        _messageIndex[message.Id] = message;
    }

    public override void Merge(ICrdt<IReadOnlyList<ChatMessage>> other)
    {
        if (other is not ChatRoom otherRoom)
            throw new ArgumentException("Can only merge with another ChatRoom");

        if (otherRoom.Id != Id)
            throw new ArgumentException("Cannot merge rooms with different IDs");

        var changed = false;

        // Merge messages
        foreach (var (_, otherMessage) in otherRoom._messageIndex)
        {
            if (_messageIndex.TryGetValue(otherMessage.Id, out var existingMessage))
            {
                // Merge: apply edits and deletions
                if (otherMessage.IsDeleted && !existingMessage.IsDeleted)
                {
                    existingMessage.IsDeleted = true;
                    changed = true;
                }

                if (otherMessage.EditedAt != null)
                {
                    if (existingMessage.EditedAt == null || otherMessage.EditedAt > existingMessage.EditedAt)
                    {
                        existingMessage.Content = otherMessage.Content;
                        existingMessage.EditedAt = otherMessage.EditedAt;
                        changed = true;
                    }
                }
            }
            else
            {
                // Add new message
                AddMessage(new ChatMessage
                {
                    Id = otherMessage.Id,
                    RoomId = otherMessage.RoomId,
                    SenderId = otherMessage.SenderId,
                    SenderName = otherMessage.SenderName,
                    Content = otherMessage.Content,
                    Timestamp = otherMessage.Timestamp,
                    IsDeleted = otherMessage.IsDeleted,
                    ReplyToId = otherMessage.ReplyToId,
                    Type = otherMessage.Type,
                    EditedAt = otherMessage.EditedAt
                });
                changed = true;

                // Update last timestamp
                if (otherMessage.Timestamp > _lastTimestamp)
                {
                    _lastTimestamp = otherMessage.Timestamp;
                }
            }
        }

        // Merge participants
        foreach (var participant in otherRoom._participants)
        {
            if (_participants.Add(participant))
            {
                changed = true;
            }
        }

        Clock = Clock.Merge(otherRoom.Clock);

        if (changed)
        {
            NotifyValueChanged();
        }
    }

    public override byte[] GetStateVector()
    {
        return MessagePackSerializer.Serialize(Clock);
    }

    public override byte[] EncodeDelta(byte[] sinceStateVector)
    {
        var state = new ChatRoomState
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Messages = _messageIndex.Values.ToList(),
            Participants = _participants.ToList(),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<ChatRoomState>(delta);
        var changed = false;

        // Apply messages
        foreach (var message in state.Messages)
        {
            if (_messageIndex.TryGetValue(message.Id, out var existing))
            {
                if (message.IsDeleted && !existing.IsDeleted)
                {
                    existing.IsDeleted = true;
                    changed = true;
                }

                if (message.EditedAt != null)
                {
                    if (existing.EditedAt == null || message.EditedAt > existing.EditedAt)
                    {
                        existing.Content = message.Content;
                        existing.EditedAt = message.EditedAt;
                        changed = true;
                    }
                }
            }
            else
            {
                AddMessage(message);
                changed = true;
                MessageReceived?.Invoke(message);

                if (message.Timestamp > _lastTimestamp)
                {
                    _lastTimestamp = message.Timestamp;
                }
            }
        }

        // Apply participants
        foreach (var participant in state.Participants)
        {
            if (_participants.Add(participant))
            {
                changed = true;
            }
        }

        Clock = Clock.Merge(state.Clock);

        if (changed)
        {
            NotifyValueChanged();
        }
    }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class ChatRoomState
{
    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = "";

    [Key(2)]
    public string Description { get; set; } = "";

    [Key(3)]
    public List<ChatMessage> Messages { get; set; } = new();

    [Key(4)]
    public List<string> Participants { get; set; } = new();

    [Key(5)]
    public VectorClock Clock { get; set; } = new();
}
