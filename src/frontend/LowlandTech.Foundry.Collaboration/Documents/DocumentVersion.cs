namespace LowlandTech.Foundry.Collaboration.Documents;

/// <summary>
/// Represents a version/snapshot of a collaborative document.
/// </summary>
public sealed class DocumentVersion
{
    /// <summary>
    /// Unique identifier for this version.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The document this version belongs to.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// The peer ID who created this version.
    /// </summary>
    public string AuthorId { get; set; } = "";

    /// <summary>
    /// Display name of the author.
    /// </summary>
    public string AuthorName { get; set; } = "";

    /// <summary>
    /// When this version was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Description of changes in this version.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The state vector at this version (for CRDT sync).
    /// </summary>
    public byte[]? StateVector { get; set; }

    /// <summary>
    /// Number of characters in the document at this version.
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Optional snapshot of the content (for quick restore).
    /// </summary>
    public string? ContentSnapshot { get; set; }
}
