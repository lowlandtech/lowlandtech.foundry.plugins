using MessagePack;

namespace LowlandTech.Foundry.P2P.Plugins;

/// <summary>
/// Metadata for a plugin available for P2P sharing.
/// </summary>
[MessagePackObject]
public sealed class PluginMetadata
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    [Key(0)]
    public string Id { get; set; } = "";

    /// <summary>
    /// Plugin display name.
    /// </summary>
    [Key(1)]
    public string Name { get; set; } = "";

    /// <summary>
    /// Plugin version (semantic versioning).
    /// </summary>
    [Key(2)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin description.
    /// </summary>
    [Key(3)]
    public string Description { get; set; } = "";

    /// <summary>
    /// Plugin author.
    /// </summary>
    [Key(4)]
    public string Author { get; set; } = "";

    /// <summary>
    /// SHA-256 hash of the plugin assembly for verification.
    /// </summary>
    [Key(5)]
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// Size of the plugin in bytes.
    /// </summary>
    [Key(6)]
    public long Size { get; set; }

    /// <summary>
    /// When the plugin was created/published.
    /// </summary>
    [Key(7)]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the plugin was last updated.
    /// </summary>
    [Key(8)]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The peer who is sharing this plugin.
    /// </summary>
    [Key(9)]
    public string SharedByPeerId { get; set; } = "";

    /// <summary>
    /// Digital signature of the plugin (signed by author's private key).
    /// </summary>
    [Key(10)]
    public byte[] Signature { get; set; } = [];

    /// <summary>
    /// Author's public key for signature verification.
    /// </summary>
    [Key(11)]
    public byte[] AuthorPublicKey { get; set; } = [];

    /// <summary>
    /// Plugin dependencies (other plugin IDs with version requirements).
    /// </summary>
    [Key(12)]
    public Dictionary<string, string> Dependencies { get; set; } = new();

    /// <summary>
    /// Tags/categories for the plugin.
    /// </summary>
    [Key(13)]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Minimum host version required.
    /// </summary>
    [Key(14)]
    public string? MinHostVersion { get; set; }

    /// <summary>
    /// Maximum host version supported.
    /// </summary>
    [Key(15)]
    public string? MaxHostVersion { get; set; }

    /// <summary>
    /// Plugin icon as base64-encoded image.
    /// </summary>
    [Key(16)]
    public string? IconBase64 { get; set; }

    /// <summary>
    /// URL to plugin's homepage or repository.
    /// </summary>
    [Key(17)]
    public string? HomepageUrl { get; set; }

    /// <summary>
    /// License identifier (e.g., "MIT", "Apache-2.0").
    /// </summary>
    [Key(18)]
    public string? License { get; set; }

    /// <summary>
    /// Whether this plugin has been verified/trusted by the local user.
    /// </summary>
    [IgnoreMember]
    public PluginTrustLevel TrustLevel { get; set; } = PluginTrustLevel.Unknown;
}

/// <summary>
/// Trust level for a plugin.
/// </summary>
public enum PluginTrustLevel
{
    /// <summary>
    /// Plugin trust has not been evaluated.
    /// </summary>
    Unknown,

    /// <summary>
    /// Plugin is from an untrusted source.
    /// </summary>
    Untrusted,

    /// <summary>
    /// Plugin signature has been verified.
    /// </summary>
    Verified,

    /// <summary>
    /// Plugin is explicitly trusted by user.
    /// </summary>
    Trusted,

    /// <summary>
    /// Plugin is blocked/blacklisted.
    /// </summary>
    Blocked
}

/// <summary>
/// Represents a plugin available in the P2P network.
/// </summary>
[MessagePackObject]
public sealed class AvailablePlugin
{
    /// <summary>
    /// Plugin metadata.
    /// </summary>
    [Key(0)]
    public PluginMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Peers that have this plugin available.
    /// </summary>
    [Key(1)]
    public List<string> AvailableFromPeers { get; set; } = new();

    /// <summary>
    /// When this entry was last updated.
    /// </summary>
    [Key(2)]
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Download count (local tracking).
    /// </summary>
    [Key(3)]
    public int DownloadCount { get; set; }
}
