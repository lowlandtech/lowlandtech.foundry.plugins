using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Plugins;

/// <summary>
/// P2P plugin catalog that discovers and manages plugins available from peers.
/// </summary>
public sealed class P2PPluginCatalog : IAsyncDisposable
{
    private readonly PeerIdentity _localIdentity;
    private readonly PluginValidator _validator;
    private readonly ChunkedPluginTransfer _transfer;
    private readonly ILogger<P2PPluginCatalog>? _logger;

    private readonly ConcurrentDictionary<string, AvailablePlugin> _availablePlugins = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _localPlugins = new();
    private readonly ConcurrentDictionary<string, PeerConnection> _connections = new();
    private readonly string _pluginStoragePath;

    /// <summary>
    /// Gets all available plugins from peers.
    /// </summary>
    public IReadOnlyDictionary<string, AvailablePlugin> AvailablePlugins => _availablePlugins;

    /// <summary>
    /// Gets locally shared plugins.
    /// </summary>
    public IReadOnlyDictionary<string, PluginMetadata> LocalPlugins => _localPlugins;

    /// <summary>
    /// Raised when a new plugin becomes available.
    /// </summary>
    public event Action<AvailablePlugin>? PluginDiscovered;

    /// <summary>
    /// Raised when a plugin is no longer available.
    /// </summary>
    public event Action<string>? PluginUnavailable;

    /// <summary>
    /// Raised when a plugin download completes.
    /// </summary>
    public event Action<string, byte[]>? PluginDownloaded;

    /// <summary>
    /// Raised when a plugin download fails.
    /// </summary>
    public event Action<string, string>? DownloadFailed;

    public P2PPluginCatalog(
        PeerIdentity localIdentity,
        string pluginStoragePath,
        PluginValidator? validator = null,
        ILogger<P2PPluginCatalog>? logger = null)
    {
        _localIdentity = localIdentity;
        _pluginStoragePath = pluginStoragePath;
        _validator = validator ?? new PluginValidator();
        _logger = logger;

        _transfer = new ChunkedPluginTransfer(localIdentity, logger: null);
        _transfer.TransferCompleted += OnTransferCompleted;
        _transfer.TransferFailed += OnTransferFailed;

        // Ensure storage directory exists
        Directory.CreateDirectory(pluginStoragePath);

        _logger?.LogInformation("P2P Plugin Catalog initialized at {Path}", pluginStoragePath);
    }

    /// <summary>
    /// Registers a peer connection for plugin sharing.
    /// </summary>
    public void RegisterConnection(PeerConnection connection)
    {
        var peerId = connection.RemotePeerId.ToBase58();
        _connections[peerId] = connection;

        connection.MessageReceived += async data =>
        {
            try
            {
                await HandleMessageAsync(peerId, data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling plugin message from {PeerId}", peerId);
            }
        };

        // Request catalog from new peer
        _ = RequestCatalogAsync(connection);

        _logger?.LogInformation("Registered peer {PeerId} for plugin sharing", peerId);
    }

    /// <summary>
    /// Unregisters a peer connection.
    /// </summary>
    public void UnregisterConnection(PeerId peerId)
    {
        var peerIdStr = peerId.ToBase58();
        _connections.TryRemove(peerIdStr, out _);

        // Remove plugins that were only available from this peer
        foreach (var (pluginId, plugin) in _availablePlugins)
        {
            plugin.AvailableFromPeers.Remove(peerIdStr);
            if (plugin.AvailableFromPeers.Count == 0)
            {
                _availablePlugins.TryRemove(pluginId, out _);
                PluginUnavailable?.Invoke(pluginId);
            }
        }

        _logger?.LogInformation("Unregistered peer {PeerId}", peerIdStr);
    }

    /// <summary>
    /// Shares a local plugin with the network.
    /// </summary>
    public async Task SharePluginAsync(string pluginPath, PluginMetadata metadata)
    {
        var pluginData = await File.ReadAllBytesAsync(pluginPath);

        // Update metadata
        metadata.ContentHash = PluginValidator.ComputeHash(pluginData);
        metadata.Size = pluginData.Length;
        metadata.SharedByPeerId = _localIdentity.Id.ToBase58();
        metadata.UpdatedAt = DateTimeOffset.UtcNow;

        _localPlugins[metadata.Id] = metadata;
        _transfer.RegisterPlugin(metadata.Id, pluginData);

        // Broadcast to all connected peers
        await BroadcastCatalogUpdateAsync();

        _logger?.LogInformation("Sharing plugin {PluginId} ({Name} v{Version})",
            metadata.Id, metadata.Name, metadata.Version);
    }

    /// <summary>
    /// Stops sharing a local plugin.
    /// </summary>
    public async Task UnsharePluginAsync(string pluginId)
    {
        _localPlugins.TryRemove(pluginId, out _);
        _transfer.UnregisterPlugin(pluginId);

        await BroadcastCatalogUpdateAsync();

        _logger?.LogInformation("Stopped sharing plugin {PluginId}", pluginId);
    }

    /// <summary>
    /// Downloads a plugin from the network.
    /// </summary>
    public async Task<string> DownloadPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!_availablePlugins.TryGetValue(pluginId, out var plugin))
        {
            throw new InvalidOperationException($"Plugin {pluginId} not found in catalog");
        }

        if (plugin.AvailableFromPeers.Count == 0)
        {
            throw new InvalidOperationException($"Plugin {pluginId} has no available sources");
        }

        // Try to download from first available peer
        foreach (var peerId in plugin.AvailableFromPeers)
        {
            if (!_connections.TryGetValue(peerId, out var connection))
                continue;

            try
            {
                var sessionId = await _transfer.StartDownloadAsync(
                    connection, plugin.Metadata, cancellationToken);

                _logger?.LogInformation("Started download of {PluginId} from {PeerId}, session {SessionId}",
                    pluginId, peerId, sessionId);

                return sessionId;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to start download from {PeerId}", peerId);
            }
        }

        throw new InvalidOperationException($"Could not start download for plugin {pluginId}");
    }

    /// <summary>
    /// Gets download progress for a transfer.
    /// </summary>
    public TransferProgress? GetDownloadProgress(string sessionId)
    {
        return _transfer.GetProgress(sessionId);
    }

    /// <summary>
    /// Cancels a download.
    /// </summary>
    public Task CancelDownloadAsync(string sessionId)
    {
        return _transfer.CancelDownloadAsync(sessionId);
    }

    /// <summary>
    /// Searches available plugins.
    /// </summary>
    public IEnumerable<AvailablePlugin> Search(
        string? query = null,
        IEnumerable<string>? tags = null,
        PluginTrustLevel? minTrustLevel = null)
    {
        var results = _availablePlugins.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLowerInvariant();
            results = results.Where(p =>
                p.Metadata.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Metadata.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Metadata.Author.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Metadata.Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)));
        }

        if (tags != null && tags.Any())
        {
            var tagSet = tags.Select(t => t.ToLowerInvariant()).ToHashSet();
            results = results.Where(p =>
                p.Metadata.Tags.Any(t => tagSet.Contains(t.ToLowerInvariant())));
        }

        if (minTrustLevel.HasValue)
        {
            results = results.Where(p => p.Metadata.TrustLevel >= minTrustLevel.Value);
        }

        return results.OrderByDescending(p => p.LastSeen);
    }

    private async Task RequestCatalogAsync(PeerConnection connection)
    {
        var request = new CatalogMessage
        {
            Type = CatalogMessageType.Request,
            SenderId = _localIdentity.Id.ToBase58()
        };

        await SendCatalogMessageAsync(connection, request);
    }

    private async Task BroadcastCatalogUpdateAsync()
    {
        var update = new CatalogMessage
        {
            Type = CatalogMessageType.Update,
            SenderId = _localIdentity.Id.ToBase58(),
            Plugins = _localPlugins.Values.ToList()
        };

        foreach (var (_, connection) in _connections)
        {
            try
            {
                await SendCatalogMessageAsync(connection, update);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send catalog update to peer");
            }
        }
    }

    private async Task HandleMessageAsync(string fromPeerId, ReadOnlyMemory<byte> data)
    {
        var envelope = MessagePackSerializer.Deserialize<PluginEnvelope>(data);

        switch (envelope.Type)
        {
            case PluginMessageType.Catalog:
                HandleCatalogMessage(fromPeerId, envelope.Payload);
                break;

            case PluginMessageType.Transfer:
                if (_connections.TryGetValue(fromPeerId, out var conn))
                {
                    await _transfer.HandleMessageAsync(conn, envelope.Payload);
                }
                break;
        }
    }

    private void HandleCatalogMessage(string fromPeerId, byte[] payload)
    {
        var message = MessagePackSerializer.Deserialize<CatalogMessage>(payload);

        switch (message.Type)
        {
            case CatalogMessageType.Request:
                // Send our catalog to the requesting peer
                if (_connections.TryGetValue(fromPeerId, out var connection))
                {
                    var response = new CatalogMessage
                    {
                        Type = CatalogMessageType.Response,
                        SenderId = _localIdentity.Id.ToBase58(),
                        Plugins = _localPlugins.Values.ToList()
                    };

                    _ = SendCatalogMessageAsync(connection, response);
                }
                break;

            case CatalogMessageType.Response:
            case CatalogMessageType.Update:
                // Update our catalog with peer's plugins
                foreach (var metadata in message.Plugins)
                {
                    if (_availablePlugins.TryGetValue(metadata.Id, out var existing))
                    {
                        // Update existing entry
                        if (!existing.AvailableFromPeers.Contains(fromPeerId))
                        {
                            existing.AvailableFromPeers.Add(fromPeerId);
                        }

                        // Use newer version if available
                        if (CompareVersions(metadata.Version, existing.Metadata.Version) > 0)
                        {
                            existing.Metadata = metadata;
                        }

                        existing.LastSeen = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        // Add new plugin
                        var available = new AvailablePlugin
                        {
                            Metadata = metadata,
                            AvailableFromPeers = new List<string> { fromPeerId },
                            LastSeen = DateTimeOffset.UtcNow
                        };

                        _availablePlugins[metadata.Id] = available;
                        PluginDiscovered?.Invoke(available);

                        _logger?.LogInformation("Discovered plugin {PluginId} ({Name}) from {PeerId}",
                            metadata.Id, metadata.Name, fromPeerId);
                    }
                }
                break;
        }
    }

    private async Task SendCatalogMessageAsync(PeerConnection connection, CatalogMessage message)
    {
        var envelope = new PluginEnvelope
        {
            Type = PluginMessageType.Catalog,
            Payload = MessagePackSerializer.Serialize(message)
        };

        await connection.SendAsync(MessagePackSerializer.Serialize(envelope));
    }

    private void OnTransferCompleted(string pluginId, byte[] data)
    {
        if (!_availablePlugins.TryGetValue(pluginId, out var plugin))
        {
            return;
        }

        // Validate the plugin
        var result = _validator.Validate(plugin.Metadata, data);
        if (!result.IsValid)
        {
            _logger?.LogError("Plugin {PluginId} validation failed: {Errors}",
                pluginId, string.Join(", ", result.Errors));
            DownloadFailed?.Invoke(pluginId, $"Validation failed: {string.Join(", ", result.Errors)}");
            return;
        }

        plugin.Metadata.TrustLevel = result.TrustLevel;

        // Save to storage
        var filePath = Path.Combine(_pluginStoragePath, $"{pluginId}.dll");
        File.WriteAllBytes(filePath, data);

        _logger?.LogInformation("Plugin {PluginId} downloaded and saved to {Path}", pluginId, filePath);
        PluginDownloaded?.Invoke(pluginId, data);
    }

    private void OnTransferFailed(string sessionId, string error)
    {
        _logger?.LogError("Transfer {SessionId} failed: {Error}", sessionId, error);
        DownloadFailed?.Invoke(sessionId, error);
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(p => int.TryParse(p.Split('-')[0], out var n) ? n : 0).ToArray();
        var parts2 = v2.Split('.').Select(p => int.TryParse(p.Split('-')[0], out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;

            if (p1 != p2)
                return p1.CompareTo(p2);
        }

        return 0;
    }

    public async ValueTask DisposeAsync()
    {
        await _transfer.DisposeAsync();
        _connections.Clear();
        _availablePlugins.Clear();
        _localPlugins.Clear();

        _logger?.LogInformation("P2P Plugin Catalog disposed");
    }
}

internal enum PluginMessageType
{
    Catalog = 0,
    Transfer = 1
}

internal enum CatalogMessageType
{
    Request = 0,
    Response = 1,
    Update = 2
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class PluginEnvelope
{
    [Key(0)]
    public PluginMessageType Type { get; set; }

    [Key(1)]
    public byte[] Payload { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class CatalogMessage
{
    [Key(0)]
    public CatalogMessageType Type { get; set; }

    [Key(1)]
    public string SenderId { get; set; } = "";

    [Key(2)]
    public List<PluginMetadata> Plugins { get; set; } = new();
}
