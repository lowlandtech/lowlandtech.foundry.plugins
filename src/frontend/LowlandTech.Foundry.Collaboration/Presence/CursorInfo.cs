namespace LowlandTech.Foundry.Collaboration.Presence;

/// <summary>
/// Extended cursor information for UI display.
/// </summary>
public sealed class CursorInfo
{
    /// <summary>
    /// The cursor position details.
    /// </summary>
    public CursorPosition Position { get; set; } = new();

    /// <summary>
    /// The color assigned to this cursor.
    /// </summary>
    public string Color { get; set; } = "#007bff";

    /// <summary>
    /// When this cursor was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the cursor is currently visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Creates a CursorInfo from a CursorPosition.
    /// </summary>
    public static CursorInfo FromPosition(CursorPosition position)
    {
        return new CursorInfo
        {
            Position = position,
            Color = position.Color,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}
