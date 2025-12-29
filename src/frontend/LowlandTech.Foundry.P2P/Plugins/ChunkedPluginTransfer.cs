using System.Collections.Concurrent;
using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Plugins;

/// <summary>
/// Handles chunked plugin transfers for resumable downloads.
/// </summary>
public sealed class ChunkedPluginTransfer : IAsyncDisposable
{
    private readonly PeerIdentity _localIdentity;
    private readonly ILogger<ChunkedPluginTransfer>? _logger;
    private readonly ConcurrentDictionary<string, TransferSession> _activeSessions = new();
    private readonly ConcurrentDictionary<string, byte[]> _localPlugins = new();
    private readonly int _chunkSize;

    /// <summary>
    /// Default chunk size (64 KB).
    /// </summary>
    public const int DefaultChunkSize = 64 * 1024;

    /// <summary>
    /// Raised when a transfer completes successfully.
    /// </summary>
    public event Action<string, byte[]>? TransferCompleted;

    /// <summary>
    /// Raised when a transfer fails.
    /// </summary>
    public event Action<string, string>? TransferFailed;

    /// <summary>
    /// Raised when transfer progress updates.
    /// </summary>
    public event Action<string, TransferProgress>? ProgressUpdated;

    public ChunkedPluginTransfer(
        PeerIdentity localIdentity,
        int chunkSize = DefaultChunkSize,
        ILogger<ChunkedPluginTransfer>? logger = null)
    {
        _localIdentity = localIdentity;
        _chunkSize = chunkSize;
        _logger = logger;
    }

    /// <summary>
    /// Registers a local plugin for sharing.
    /// </summary>
    public void RegisterPlugin(string pluginId, byte[] pluginData)
    {
        _localPlugins[pluginId] = pluginData;
        _logger?.LogInformation("Registered plugin {PluginId} for sharing ({Size} bytes)",
            pluginId, pluginData.Length);
    }

    /// <summary>
    /// Unregisters a local plugin.
    /// </summary>
    public void UnregisterPlugin(string pluginId)
    {
        _localPlugins.TryRemove(pluginId, out _);
    }

    /// <summary>
    /// Starts downloading a plugin from a peer.
    /// </summary>
    public async Task<string> StartDownloadAsync(
        PeerConnection connection,
        PluginMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();

        var session = new TransferSession
        {
            SessionId = sessionId,
            PluginId = metadata.Id,
            TotalSize = metadata.Size,
            ExpectedHash = metadata.ContentHash,
            Direction = TransferDirection.Download,
            Connection = connection,
            StartedAt = DateTimeOffset.UtcNow
        };

        _activeSessions[sessionId] = session;

        _logger?.LogInformation("Starting download of plugin {PluginId} from {PeerId}, session {SessionId}",
            metadata.Id, connection.RemotePeerId, sessionId);

        // Send download request
        var request = new TransferRequest
        {
            SessionId = sessionId,
            PluginId = metadata.Id,
            RequestType = TransferRequestType.Start
        };

        await SendMessageAsync(connection, request);

        return sessionId;
    }

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    public async Task ResumeDownloadAsync(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (session.Connection == null)
        {
            throw new InvalidOperationException("Session has no active connection");
        }

        session.IsPaused = false;

        var request = new TransferRequest
        {
            SessionId = sessionId,
            PluginId = session.PluginId,
            RequestType = TransferRequestType.Resume,
            ResumeFromChunk = session.ReceivedChunks.Count
        };

        await SendMessageAsync(session.Connection, request);
    }

    /// <summary>
    /// Pauses an active download.
    /// </summary>
    public void PauseDownload(string sessionId)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            session.IsPaused = true;
            _logger?.LogInformation("Paused download session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Cancels a download.
    /// </summary>
    public async Task CancelDownloadAsync(string sessionId)
    {
        if (_activeSessions.TryRemove(sessionId, out var session))
        {
            if (session.Connection != null)
            {
                var request = new TransferRequest
                {
                    SessionId = sessionId,
                    PluginId = session.PluginId,
                    RequestType = TransferRequestType.Cancel
                };

                await SendMessageAsync(session.Connection, request);
            }

            _logger?.LogInformation("Cancelled download session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Gets the current progress of a transfer.
    /// </summary>
    public TransferProgress? GetProgress(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
            return null;

        var totalChunks = (int)Math.Ceiling((double)session.TotalSize / _chunkSize);
        var receivedBytes = session.ReceivedChunks.Count * _chunkSize;

        return new TransferProgress
        {
            SessionId = sessionId,
            PluginId = session.PluginId,
            TotalBytes = session.TotalSize,
            TransferredBytes = Math.Min(receivedBytes, session.TotalSize),
            TotalChunks = totalChunks,
            TransferredChunks = session.ReceivedChunks.Count,
            BytesPerSecond = CalculateSpeed(session),
            IsPaused = session.IsPaused,
            StartedAt = session.StartedAt
        };
    }

    /// <summary>
    /// Handles an incoming transfer message.
    /// </summary>
    public async Task HandleMessageAsync(PeerConnection connection, ReadOnlyMemory<byte> data)
    {
        var envelope = MessagePackSerializer.Deserialize<TransferEnvelope>(data);

        switch (envelope.Type)
        {
            case TransferMessageType.Request:
                await HandleRequestAsync(connection, envelope.Payload);
                break;

            case TransferMessageType.Chunk:
                await HandleChunkAsync(connection, envelope.Payload);
                break;

            case TransferMessageType.Complete:
                await HandleCompleteAsync(envelope.Payload);
                break;

            case TransferMessageType.Error:
                HandleError(envelope.Payload);
                break;
        }
    }

    private async Task HandleRequestAsync(PeerConnection connection, byte[] payload)
    {
        var request = MessagePackSerializer.Deserialize<TransferRequest>(payload);

        switch (request.RequestType)
        {
            case TransferRequestType.Start:
            case TransferRequestType.Resume:
                await HandleStartRequestAsync(connection, request);
                break;

            case TransferRequestType.Cancel:
                _activeSessions.TryRemove(request.SessionId, out _);
                break;
        }
    }

    private async Task HandleStartRequestAsync(PeerConnection connection, TransferRequest request)
    {
        if (!_localPlugins.TryGetValue(request.PluginId, out var pluginData))
        {
            await SendErrorAsync(connection, request.SessionId, "Plugin not available");
            return;
        }

        var session = new TransferSession
        {
            SessionId = request.SessionId,
            PluginId = request.PluginId,
            TotalSize = pluginData.Length,
            Direction = TransferDirection.Upload,
            Connection = connection,
            StartedAt = DateTimeOffset.UtcNow
        };

        _activeSessions[request.SessionId] = session;

        var startChunk = request.ResumeFromChunk;
        var totalChunks = (int)Math.Ceiling((double)pluginData.Length / _chunkSize);

        _logger?.LogInformation("Starting upload of plugin {PluginId} to {PeerId}, chunks {Start}-{Total}",
            request.PluginId, connection.RemotePeerId, startChunk, totalChunks);

        // Send chunks
        for (var i = startChunk; i < totalChunks; i++)
        {
            if (!_activeSessions.ContainsKey(request.SessionId))
            {
                // Session was cancelled
                return;
            }

            var offset = i * _chunkSize;
            var length = Math.Min(_chunkSize, pluginData.Length - offset);
            var chunkData = new byte[length];
            Array.Copy(pluginData, offset, chunkData, 0, length);

            var chunk = new TransferChunk
            {
                SessionId = request.SessionId,
                ChunkIndex = i,
                TotalChunks = totalChunks,
                Data = chunkData
            };

            await SendChunkAsync(connection, chunk);

            // Small delay to prevent overwhelming the connection
            await Task.Delay(1);
        }

        // Send completion message
        var complete = new TransferComplete
        {
            SessionId = request.SessionId,
            PluginId = request.PluginId,
            ContentHash = PluginValidator.ComputeHash(pluginData)
        };

        await SendCompleteAsync(connection, complete);
        _activeSessions.TryRemove(request.SessionId, out _);
    }

    private Task HandleChunkAsync(PeerConnection connection, byte[] payload)
    {
        var chunk = MessagePackSerializer.Deserialize<TransferChunk>(payload);

        if (!_activeSessions.TryGetValue(chunk.SessionId, out var session))
        {
            return Task.CompletedTask;
        }

        if (session.IsPaused)
        {
            return Task.CompletedTask;
        }

        session.ReceivedChunks[chunk.ChunkIndex] = chunk.Data;
        session.LastActivityAt = DateTimeOffset.UtcNow;

        // Update progress
        ProgressUpdated?.Invoke(chunk.SessionId, GetProgress(chunk.SessionId)!);

        return Task.CompletedTask;
    }

    private Task HandleCompleteAsync(byte[] payload)
    {
        var complete = MessagePackSerializer.Deserialize<TransferComplete>(payload);

        if (!_activeSessions.TryRemove(complete.SessionId, out var session))
        {
            return Task.CompletedTask;
        }

        // Reassemble data
        var data = ReassembleData(session);

        // Verify hash
        var computedHash = PluginValidator.ComputeHash(data);
        if (computedHash != complete.ContentHash)
        {
            _logger?.LogError("Transfer {SessionId} hash mismatch", complete.SessionId);
            TransferFailed?.Invoke(complete.SessionId, "Hash verification failed");
            return Task.CompletedTask;
        }

        _logger?.LogInformation("Transfer {SessionId} completed successfully", complete.SessionId);
        TransferCompleted?.Invoke(complete.PluginId, data);

        return Task.CompletedTask;
    }

    private void HandleError(byte[] payload)
    {
        var error = MessagePackSerializer.Deserialize<TransferError>(payload);

        _activeSessions.TryRemove(error.SessionId, out _);
        _logger?.LogError("Transfer {SessionId} failed: {Message}", error.SessionId, error.Message);
        TransferFailed?.Invoke(error.SessionId, error.Message);
    }

    private byte[] ReassembleData(TransferSession session)
    {
        var chunks = session.ReceivedChunks.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        var totalLength = chunks.Sum(c => c.Length);
        var result = new byte[totalLength];

        var offset = 0;
        foreach (var chunk in chunks)
        {
            Array.Copy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    private static double CalculateSpeed(TransferSession session)
    {
        var elapsed = DateTimeOffset.UtcNow - session.StartedAt;
        if (elapsed.TotalSeconds < 1)
            return 0;

        var bytesReceived = session.ReceivedChunks.Sum(c => c.Value.Length);
        return bytesReceived / elapsed.TotalSeconds;
    }

    private async Task SendMessageAsync(PeerConnection connection, TransferRequest request)
    {
        var envelope = new TransferEnvelope
        {
            Type = TransferMessageType.Request,
            Payload = MessagePackSerializer.Serialize(request)
        };

        await connection.SendAsync(MessagePackSerializer.Serialize(envelope));
    }

    private async Task SendChunkAsync(PeerConnection connection, TransferChunk chunk)
    {
        var envelope = new TransferEnvelope
        {
            Type = TransferMessageType.Chunk,
            Payload = MessagePackSerializer.Serialize(chunk)
        };

        await connection.SendAsync(MessagePackSerializer.Serialize(envelope));
    }

    private async Task SendCompleteAsync(PeerConnection connection, TransferComplete complete)
    {
        var envelope = new TransferEnvelope
        {
            Type = TransferMessageType.Complete,
            Payload = MessagePackSerializer.Serialize(complete)
        };

        await connection.SendAsync(MessagePackSerializer.Serialize(envelope));
    }

    private async Task SendErrorAsync(PeerConnection connection, string sessionId, string message)
    {
        var error = new TransferError { SessionId = sessionId, Message = message };
        var envelope = new TransferEnvelope
        {
            Type = TransferMessageType.Error,
            Payload = MessagePackSerializer.Serialize(error)
        };

        await connection.SendAsync(MessagePackSerializer.Serialize(envelope));
    }

    public ValueTask DisposeAsync()
    {
        _activeSessions.Clear();
        _localPlugins.Clear();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Transfer progress information.
/// </summary>
public sealed class TransferProgress
{
    public string SessionId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public int TotalChunks { get; set; }
    public int TransferredChunks { get; set; }
    public double BytesPerSecond { get; set; }
    public bool IsPaused { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    public double PercentComplete => TotalBytes > 0 ? (double)TransferredBytes / TotalBytes * 100 : 0;

    public TimeSpan EstimatedTimeRemaining
    {
        get
        {
            if (BytesPerSecond <= 0)
                return TimeSpan.MaxValue;

            var remainingBytes = TotalBytes - TransferredBytes;
            return TimeSpan.FromSeconds(remainingBytes / BytesPerSecond);
        }
    }
}

internal sealed class TransferSession
{
    public string SessionId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public long TotalSize { get; set; }
    public string ExpectedHash { get; set; } = "";
    public TransferDirection Direction { get; set; }
    public PeerConnection? Connection { get; set; }
    public ConcurrentDictionary<int, byte[]> ReceivedChunks { get; } = new();
    public bool IsPaused { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
}

internal enum TransferDirection
{
    Download,
    Upload
}

internal enum TransferMessageType
{
    Request = 0,
    Chunk = 1,
    Complete = 2,
    Error = 3
}

internal enum TransferRequestType
{
    Start = 0,
    Resume = 1,
    Cancel = 2
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class TransferEnvelope
{
    [Key(0)]
    public TransferMessageType Type { get; set; }

    [Key(1)]
    public byte[] Payload { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class TransferRequest
{
    [Key(0)]
    public string SessionId { get; set; } = "";

    [Key(1)]
    public string PluginId { get; set; } = "";

    [Key(2)]
    public TransferRequestType RequestType { get; set; }

    [Key(3)]
    public int ResumeFromChunk { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class TransferChunk
{
    [Key(0)]
    public string SessionId { get; set; } = "";

    [Key(1)]
    public int ChunkIndex { get; set; }

    [Key(2)]
    public int TotalChunks { get; set; }

    [Key(3)]
    public byte[] Data { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class TransferComplete
{
    [Key(0)]
    public string SessionId { get; set; } = "";

    [Key(1)]
    public string PluginId { get; set; } = "";

    [Key(2)]
    public string ContentHash { get; set; } = "";
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class TransferError
{
    [Key(0)]
    public string SessionId { get; set; } = "";

    [Key(1)]
    public string Message { get; set; } = "";
}
