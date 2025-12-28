using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Core.Identity;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Core.Security;

/// <summary>
/// Default implementation of trust management.
/// </summary>
public sealed class TrustManager : ITrustManager
{
    private readonly ConcurrentDictionary<PeerId, TrustRecord> _trustRecords = new();
    private readonly TrustManagerOptions _options;
    private readonly ILogger<TrustManager>? _logger;

    public event Func<PeerId, TrustLevel, Task>? TrustLevelChanged;

    public TrustManager(
        TrustManagerOptions? options = null,
        ILogger<TrustManager>? logger = null)
    {
        _options = options ?? new TrustManagerOptions();
        _logger = logger;
    }

    public TrustLevel GetTrustLevel(PeerId peerId)
    {
        if (_trustRecords.TryGetValue(peerId, out var record))
        {
            return record.TrustLevel;
        }

        return TrustLevel.Unknown;
    }

    public async Task SetTrustLevelAsync(PeerId peerId, TrustLevel level)
    {
        var now = DateTimeOffset.UtcNow;

        if (_trustRecords.TryGetValue(peerId, out var existing))
        {
            var updated = existing with
            {
                TrustLevel = level,
                ModifiedAt = now,
                LastSeen = now
            };

            _trustRecords[peerId] = updated;

            _logger?.LogInformation("Updated trust level for {PeerId}: {Level}", peerId, level);
        }
        else
        {
            var record = new TrustRecord
            {
                PeerId = peerId,
                DisplayName = "Unknown",
                TrustLevel = level,
                FirstSeen = now,
                LastSeen = now,
                ModifiedAt = now
            };

            _trustRecords[peerId] = record;

            _logger?.LogInformation("Created trust record for {PeerId}: {Level}", peerId, level);
        }

        if (TrustLevelChanged != null)
        {
            await TrustLevelChanged.Invoke(peerId, level);
        }
    }

    public Task<bool> ShouldAllowConnectionAsync(PeerId peerId)
    {
        var level = GetTrustLevel(peerId);

        // Block blocked peers
        if (level == TrustLevel.Blocked)
        {
            _logger?.LogDebug("Rejecting connection from blocked peer {PeerId}", peerId);
            return Task.FromResult(false);
        }

        // Check if we require trusted peers only
        if (_options.RequireTrustedPeers && level < TrustLevel.Trusted)
        {
            _logger?.LogDebug("Rejecting connection from untrusted peer {PeerId}", peerId);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> ShouldAcceptPluginAsync(PeerId fromPeer, string pluginName)
    {
        var level = GetTrustLevel(fromPeer);

        // Never accept plugins from blocked peers
        if (level == TrustLevel.Blocked)
        {
            _logger?.LogDebug("Rejecting plugin from blocked peer {PeerId}", fromPeer);
            return Task.FromResult(false);
        }

        // Check plugin trust requirements
        if (_options.RequireTrustedForPlugins && level < TrustLevel.Trusted)
        {
            _logger?.LogDebug("Rejecting plugin from untrusted peer {PeerId}", fromPeer);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TrustRecord>> GetAllTrustRecordsAsync()
    {
        var records = _trustRecords.Values.ToList();
        return Task.FromResult<IReadOnlyList<TrustRecord>>(records);
    }

    /// <summary>
    /// Updates the last seen time for a peer and optionally sets display name.
    /// </summary>
    public void UpdatePeerSeen(PeerId peerId, string? displayName = null)
    {
        var now = DateTimeOffset.UtcNow;

        if (_trustRecords.TryGetValue(peerId, out var existing))
        {
            var updated = existing with
            {
                LastSeen = now,
                DisplayName = displayName ?? existing.DisplayName
            };

            _trustRecords[peerId] = updated;
        }
        else
        {
            // First time seeing this peer
            var record = new TrustRecord
            {
                PeerId = peerId,
                DisplayName = displayName ?? "Unknown",
                TrustLevel = TrustLevel.Unknown,
                FirstSeen = now,
                LastSeen = now,
                ModifiedAt = now
            };

            _trustRecords[peerId] = record;
        }
    }

    /// <summary>
    /// Removes a trust record completely.
    /// </summary>
    public bool RemoveTrustRecord(PeerId peerId)
    {
        return _trustRecords.TryRemove(peerId, out _);
    }

    /// <summary>
    /// Gets trusted peers only.
    /// </summary>
    public IReadOnlyList<TrustRecord> GetTrustedPeers()
    {
        return _trustRecords.Values
            .Where(r => r.TrustLevel >= TrustLevel.Trusted)
            .ToList();
    }

    /// <summary>
    /// Gets blocked peers.
    /// </summary>
    public IReadOnlyList<TrustRecord> GetBlockedPeers()
    {
        return _trustRecords.Values
            .Where(r => r.TrustLevel == TrustLevel.Blocked)
            .ToList();
    }
}

/// <summary>
/// Options for TrustManager.
/// </summary>
public sealed class TrustManagerOptions
{
    /// <summary>
    /// If true, only trusted peers can connect.
    /// </summary>
    public bool RequireTrustedPeers { get; set; } = false;

    /// <summary>
    /// If true, only accept plugins from trusted peers.
    /// </summary>
    public bool RequireTrustedForPlugins { get; set; } = true;
}
