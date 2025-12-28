using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.Collaboration.Presence;

/// <summary>
/// Service for tracking and broadcasting peer presence information.
/// </summary>
public interface IPresenceService
{
    /// <summary>
    /// Gets the local peer's presence state.
    /// </summary>
    PresenceState LocalState { get; }

    /// <summary>
    /// Gets all currently tracked peer presence states.
    /// </summary>
    IReadOnlyDictionary<string, PresenceState> PeerStates { get; }

    /// <summary>
    /// Updates the local peer's presence state.
    /// </summary>
    void UpdateLocalState(Action<PresenceState> update);

    /// <summary>
    /// Sets the local peer's status.
    /// </summary>
    void SetStatus(PresenceStatus status, string? statusMessage = null);

    /// <summary>
    /// Sets the local peer's current activity.
    /// </summary>
    void SetActivity(PresenceActivity activity);

    /// <summary>
    /// Sets cursor position for collaborative editing.
    /// </summary>
    void SetCursor(Guid documentId, int position, int? selectionEnd = null);

    /// <summary>
    /// Clears cursor position.
    /// </summary>
    void ClearCursor();

    /// <summary>
    /// Raised when any peer's presence state changes.
    /// </summary>
    event Action<string, PresenceState>? PresenceChanged;

    /// <summary>
    /// Raised when a peer goes offline.
    /// </summary>
    event Action<string>? PeerOffline;

    /// <summary>
    /// Raised when a peer comes online.
    /// </summary>
    event Action<string, PresenceState>? PeerOnline;
}

/// <summary>
/// Represents a peer's presence state.
/// </summary>
public sealed class PresenceState
{
    /// <summary>
    /// The peer's ID.
    /// </summary>
    public string PeerId { get; set; } = "";

    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Avatar URL or identifier.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public PresenceStatus Status { get; set; } = PresenceStatus.Online;

    /// <summary>
    /// Optional status message.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Current activity.
    /// </summary>
    public PresenceActivity Activity { get; set; } = new();

    /// <summary>
    /// Cursor position for collaborative editing.
    /// </summary>
    public CursorPosition? Cursor { get; set; }

    /// <summary>
    /// When this state was last updated.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom data for application-specific presence info.
    /// </summary>
    public Dictionary<string, string> CustomData { get; set; } = new();

    /// <summary>
    /// Creates a copy of this state.
    /// </summary>
    public PresenceState Clone() => new()
    {
        PeerId = PeerId,
        DisplayName = DisplayName,
        AvatarUrl = AvatarUrl,
        Status = Status,
        StatusMessage = StatusMessage,
        Activity = Activity.Clone(),
        Cursor = Cursor?.Clone(),
        LastSeen = LastSeen,
        CustomData = new Dictionary<string, string>(CustomData)
    };
}

/// <summary>
/// Presence status levels.
/// </summary>
public enum PresenceStatus
{
    /// <summary>
    /// Peer is online and active.
    /// </summary>
    Online,

    /// <summary>
    /// Peer is online but away/idle.
    /// </summary>
    Away,

    /// <summary>
    /// Peer is busy and should not be disturbed.
    /// </summary>
    Busy,

    /// <summary>
    /// Peer appears offline but can receive messages.
    /// </summary>
    Invisible,

    /// <summary>
    /// Peer is offline.
    /// </summary>
    Offline
}

/// <summary>
/// Represents what a peer is currently doing.
/// </summary>
public sealed class PresenceActivity
{
    /// <summary>
    /// The type of activity.
    /// </summary>
    public ActivityType Type { get; set; } = ActivityType.Idle;

    /// <summary>
    /// ID of the resource being worked on (document, chat room, etc.).
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// Human-readable description of the activity.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this activity started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public PresenceActivity Clone() => new()
    {
        Type = Type,
        ResourceId = ResourceId,
        Description = Description,
        StartedAt = StartedAt
    };
}

/// <summary>
/// Types of activities.
/// </summary>
public enum ActivityType
{
    /// <summary>
    /// No specific activity.
    /// </summary>
    Idle,

    /// <summary>
    /// Editing a document.
    /// </summary>
    Editing,

    /// <summary>
    /// Viewing a document.
    /// </summary>
    Viewing,

    /// <summary>
    /// In a chat conversation.
    /// </summary>
    Chatting,

    /// <summary>
    /// Typing a message.
    /// </summary>
    Typing,

    /// <summary>
    /// In a call.
    /// </summary>
    InCall
}

/// <summary>
/// Represents cursor position in a document.
/// </summary>
public sealed class CursorPosition
{
    /// <summary>
    /// The document ID.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Cursor position (character offset).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// End of selection (if text is selected).
    /// </summary>
    public int? SelectionEnd { get; set; }

    /// <summary>
    /// Line number (1-based).
    /// </summary>
    public int Line { get; set; } = 1;

    /// <summary>
    /// Column number (1-based).
    /// </summary>
    public int Column { get; set; } = 1;

    /// <summary>
    /// Assigned cursor color for this peer.
    /// </summary>
    public string Color { get; set; } = "#007bff";

    public CursorPosition Clone() => new()
    {
        DocumentId = DocumentId,
        Position = Position,
        SelectionEnd = SelectionEnd,
        Line = Line,
        Column = Column,
        Color = Color
    };
}
