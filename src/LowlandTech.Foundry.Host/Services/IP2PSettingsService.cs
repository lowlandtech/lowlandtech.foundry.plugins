namespace LowlandTech.Foundry.Host.Services;

public interface IP2PSettingsService
{
    Task<UserP2PSettingsDto?> GetSettingsAsync();
    Task<bool> UpdateSettingsAsync(UpdateP2PSettingsDto settings);
    Task<bool> TrustPeerAsync(string peerId);
    Task<bool> BlockPeerAsync(string peerId);
    Task<bool> UntrustPeerAsync(string peerId);
    Task<bool> UnblockPeerAsync(string peerId);
}

public record UserP2PSettingsDto(
    string? PeerId,
    string? P2PDisplayName,
    string Status,
    string? StatusMessage,
    bool EnableLanDiscovery,
    bool EnableWanDiscovery,
    bool RequirePeerVerification,
    bool AutoAcceptTrustedPeers,
    bool EnablePluginSharing,
    bool AutoDownloadFromTrusted,
    bool RequireSignedPlugins,
    List<string> TrustedPeerIds,
    List<string> BlockedPeerIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateP2PSettingsDto(
    string? P2PDisplayName = null,
    string? Status = null,
    string? StatusMessage = null,
    bool? EnableLanDiscovery = null,
    bool? EnableWanDiscovery = null,
    bool? RequirePeerVerification = null,
    bool? AutoAcceptTrustedPeers = null,
    bool? EnablePluginSharing = null,
    bool? AutoDownloadFromTrusted = null,
    bool? RequireSignedPlugins = null
);
