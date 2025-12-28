using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;

namespace LowlandTech.Foundry.Collaboration.Documents;

/// <summary>
/// A collaborative document backed by the RGA CRDT for real-time text editing.
/// Supports multiple concurrent editors with automatic conflict resolution.
/// </summary>
public sealed class CollaborativeDocument : CrdtBase<string>
{
    private readonly Rga _content;
    private readonly LwwRegister<DocumentMetadata> _metadata;

    /// <summary>
    /// Gets the document title.
    /// </summary>
    public string Title => _metadata.Value?.Title ?? "Untitled";

    /// <summary>
    /// Gets the document content as text.
    /// </summary>
    public override string Value => _content.Value;

    /// <summary>
    /// Gets the document length.
    /// </summary>
    public int Length => _content.Length;

    /// <summary>
    /// Gets the document metadata.
    /// </summary>
    public DocumentMetadata? Metadata => _metadata.Value;

    /// <summary>
    /// Raised when the document content changes.
    /// </summary>
    public event Action<DocumentChange>? ContentChanged;

    public CollaborativeDocument(Guid id, PeerId localPeerId, string title = "Untitled")
        : base(id, localPeerId)
    {
        _content = new Rga(id, localPeerId);
        _metadata = new LwwRegister<DocumentMetadata>(id, localPeerId, new DocumentMetadata
        {
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = localPeerId.ToBase58()
        });

        _content.ValueChanged += _ => NotifyValueChanged();
    }

    /// <summary>
    /// Sets the document title.
    /// </summary>
    public void SetTitle(string title)
    {
        var current = _metadata.Value ?? new DocumentMetadata();
        _metadata.Set(current with { Title = title, LastModifiedAt = DateTimeOffset.UtcNow });
        IncrementClock();
    }

    /// <summary>
    /// Inserts text at the specified position.
    /// </summary>
    public void Insert(int position, string text)
    {
        if (position < 0 || position > Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _content.Insert(position, text);
        IncrementClock();

        ContentChanged?.Invoke(new DocumentChange
        {
            Type = ChangeType.Insert,
            Position = position,
            Text = text,
            Length = text.Length
        });
    }

    /// <summary>
    /// Inserts a single character at the specified position.
    /// </summary>
    public void Insert(int position, char character)
    {
        Insert(position, character.ToString());
    }

    /// <summary>
    /// Deletes text at the specified position.
    /// </summary>
    public void Delete(int position, int length = 1)
    {
        if (position < 0 || position >= Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        var deletedText = Value.Substring(position, Math.Min(length, Length - position));
        _content.Delete(position, length);
        IncrementClock();

        ContentChanged?.Invoke(new DocumentChange
        {
            Type = ChangeType.Delete,
            Position = position,
            Text = deletedText,
            Length = length
        });
    }

    /// <summary>
    /// Replaces text at the specified position.
    /// </summary>
    public void Replace(int position, int length, string newText)
    {
        Delete(position, length);
        Insert(position, newText);
    }

    /// <summary>
    /// Appends text to the end of the document.
    /// </summary>
    public void Append(string text)
    {
        Insert(Length, text);
    }

    /// <summary>
    /// Clears all content from the document.
    /// </summary>
    public void Clear()
    {
        if (Length > 0)
        {
            Delete(0, Length);
        }
    }

    /// <summary>
    /// Gets a substring of the document content.
    /// </summary>
    public string GetText(int start, int length)
    {
        if (start < 0 || start >= Length)
            return "";

        return Value.Substring(start, Math.Min(length, Length - start));
    }

    /// <summary>
    /// Gets the character at the specified position.
    /// </summary>
    public char? GetCharAt(int position)
    {
        if (position < 0 || position >= Length)
            return null;

        return Value[position];
    }

    public override void Merge(ICrdt<string> other)
    {
        if (other is not CollaborativeDocument otherDoc)
            throw new ArgumentException("Can only merge with another CollaborativeDocument");

        if (otherDoc.Id != Id)
            throw new ArgumentException("Cannot merge documents with different IDs");

        _content.Merge(otherDoc._content);
        _metadata.Merge(otherDoc._metadata);
        Clock = Clock.Merge(otherDoc.Clock);
    }

    public override byte[] GetStateVector()
    {
        return MessagePackSerializer.Serialize(Clock);
    }

    public override byte[] EncodeDelta(byte[] sinceStateVector)
    {
        var state = new CollaborativeDocumentState
        {
            ContentDelta = _content.EncodeDelta(sinceStateVector),
            MetadataDelta = _metadata.EncodeDelta(sinceStateVector),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<CollaborativeDocumentState>(delta);

        _content.ApplyDelta(state.ContentDelta);
        _metadata.ApplyDelta(state.MetadataDelta);
        Clock = Clock.Merge(state.Clock);
    }
}

/// <summary>
/// Metadata for a collaborative document.
/// </summary>
[MessagePackObject]
public sealed record DocumentMetadata
{
    [Key(0)]
    public string Title { get; set; } = "Untitled";

    [Key(1)]
    public DateTimeOffset CreatedAt { get; set; }

    [Key(2)]
    public string CreatedBy { get; set; } = "";

    [Key(3)]
    public DateTimeOffset? LastModifiedAt { get; set; }

    [Key(4)]
    public string? LastModifiedBy { get; set; }

    [Key(5)]
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}

/// <summary>
/// Represents a change to a document.
/// </summary>
public sealed class DocumentChange
{
    /// <summary>
    /// The type of change.
    /// </summary>
    public ChangeType Type { get; init; }

    /// <summary>
    /// The position where the change occurred.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// The text that was inserted or deleted.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// The length of the change.
    /// </summary>
    public int Length { get; init; }
}

/// <summary>
/// Types of document changes.
/// </summary>
public enum ChangeType
{
    Insert,
    Delete
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class CollaborativeDocumentState
{
    [Key(0)]
    public byte[] ContentDelta { get; set; } = [];

    [Key(1)]
    public byte[] MetadataDelta { get; set; } = [];

    [Key(2)]
    public VectorClock Clock { get; set; } = new();
}
