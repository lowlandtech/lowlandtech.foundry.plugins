using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;

namespace LowlandTech.Foundry.Collaboration.Chat;

/// <summary>
/// Represents a chat message in a room.
/// </summary>
[MessagePackObject]
public sealed class ChatMessage
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    [Key(0)]
    public Guid Id { get; set; }

    /// <summary>
    /// The room this message belongs to.
    /// </summary>
    [Key(1)]
    public Guid RoomId { get; set; }

    /// <summary>
    /// The peer who sent this message.
    /// </summary>
    [Key(2)]
    public string SenderId { get; set; } = "";

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    [Key(3)]
    public string SenderName { get; set; } = "";

    /// <summary>
    /// The message content.
    /// </summary>
    [Key(4)]
    public string Content { get; set; } = "";

    /// <summary>
    /// When the message was sent (HLC timestamp for ordering).
    /// </summary>
    [Key(5)]
    public HybridLogicalClock Timestamp { get; set; } = new();

    /// <summary>
    /// Whether this message has been deleted (tombstone).
    /// </summary>
    [Key(6)]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Optional: ID of message this is replying to.
    /// </summary>
    [Key(7)]
    public Guid? ReplyToId { get; set; }

    /// <summary>
    /// Message type (text, system, etc.).
    /// </summary>
    [Key(8)]
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;

    /// <summary>
    /// Last edit timestamp (null if never edited).
    /// </summary>
    [Key(9)]
    public HybridLogicalClock? EditedAt { get; set; }
}

/// <summary>
/// Types of chat messages.
/// </summary>
public enum ChatMessageType
{
    /// <summary>
    /// Regular text message.
    /// </summary>
    Text = 0,

    /// <summary>
    /// System message (user joined, left, etc.).
    /// </summary>
    System = 1,

    /// <summary>
    /// File or attachment message.
    /// </summary>
    Attachment = 2
}
