namespace LowlandTech.Foundry.PluginCore.Abstractions;

/// <summary>
/// Contains metadata about a plugin including identification, versioning, and descriptive information.
/// </summary>
/// <param name="Id">Unique identifier for the plugin (e.g., "lowlandtech.premiumtheme").</param>
/// <param name="Name">Human-readable name of the plugin.</param>
/// <param name="Description">Detailed description of what the plugin provides.</param>
/// <param name="Version">Semantic version of the plugin.</param>
/// <param name="Author">Author or organization that created the plugin.</param>
/// <param name="Tags">Categorization tags for the plugin.</param>
public record PluginMetadata(
    string Id,
    string Name,
    string Description,
    Version Version,
    string Author,
    IReadOnlyList<string> Tags
)
{
    /// <summary>
    /// Creates a new PluginMetadata with default empty tags.
    /// </summary>
    public PluginMetadata(string id, string name, string description, Version version, string author)
        : this(id, name, description, version, author, Array.Empty<string>())
    {
    }

    /// <summary>
    /// Creates a new PluginMetadata with tags specified as params.
    /// </summary>
    public static PluginMetadata Create(
        string id,
        string name,
        string description,
        Version version,
        string author,
        params string[] tags)
        => new(id, name, description, version, author, tags);
}
