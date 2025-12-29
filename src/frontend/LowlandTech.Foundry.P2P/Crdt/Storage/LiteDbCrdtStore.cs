using LiteDB;
using LowlandTech.Foundry.P2P.Crdt.Types;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Crdt.Storage;

/// <summary>
/// LiteDB-based implementation of CRDT storage for offline-capable persistence.
/// Uses a type registry to deserialize stored CRDTs back to their concrete types.
/// </summary>
public sealed class LiteDbCrdtStore : ICrdtStore
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<CrdtDocument> _collection;
    private readonly ILogger<LiteDbCrdtStore>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, Type> _typeRegistry = new();
    private bool _disposed;

    private const string CollectionName = "crdts";

    public LiteDbCrdtStore(string connectionString, ILogger<LiteDbCrdtStore>? logger = null)
    {
        _logger = logger;
        _database = new LiteDatabase(connectionString);
        _collection = _database.GetCollection<CrdtDocument>(CollectionName);
        _collection.EnsureIndex(x => x.Id, unique: true);
        _collection.EnsureIndex(x => x.TypeName);

        _logger?.LogInformation("LiteDB CRDT store initialized at {ConnectionString}", connectionString);
    }

    /// <summary>
    /// Registers a CRDT type for deserialization.
    /// </summary>
    public void RegisterType<T>() where T : class, ICrdt
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        _typeRegistry[typeName] = typeof(T);
    }

    public async Task<ICrdt?> GetAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var doc = _collection.FindById(id);
            if (doc == null)
            {
                _logger?.LogDebug("CRDT {Id} not found", id);
                return null;
            }

            if (!_typeRegistry.TryGetValue(doc.TypeName, out var type))
            {
                _logger?.LogWarning("Unknown CRDT type {TypeName} for {Id}", doc.TypeName, id);
                return null;
            }

            var crdt = DeserializeCrdt(doc, type);
            _logger?.LogDebug("Retrieved CRDT {Id} of type {Type}", id, doc.TypeName);
            return crdt as ICrdt;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> GetAsync<T>(Guid id) where T : class, ICrdt
    {
        await _lock.WaitAsync();
        try
        {
            var doc = _collection.FindById(id);
            if (doc == null)
            {
                _logger?.LogDebug("CRDT {Id} not found", id);
                return null;
            }

            var crdt = DeserializeCrdt<T>(doc);
            _logger?.LogDebug("Retrieved CRDT {Id} of type {Type}", id, typeof(T).Name);
            return crdt;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>() where T : class, ICrdt
    {
        await _lock.WaitAsync();
        try
        {
            var typeName = typeof(T).FullName ?? typeof(T).Name;
            var docs = _collection.Find(x => x.TypeName == typeName);
            var result = new List<T>();

            foreach (var doc in docs)
            {
                var crdt = DeserializeCrdt<T>(doc);
                if (crdt != null)
                {
                    result.Add(crdt);
                }
            }

            _logger?.LogDebug("Retrieved {Count} CRDTs of type {Type}", result.Count, typeof(T).Name);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ICrdt crdt)
    {
        await _lock.WaitAsync();
        try
        {
            var doc = SerializeCrdt(crdt);
            _collection.Upsert(doc);
            _logger?.LogDebug("Saved CRDT {Id} of type {Type}", doc.Id, doc.TypeName);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var deleted = _collection.Delete(id);
            _logger?.LogDebug("Deleted CRDT {Id}: {Result}", id, deleted);
            return deleted;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            return _collection.Exists(x => x.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<Guid>> GetAllIdsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _collection.FindAll().Select(x => x.Id).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]?> GetStateVectorAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var doc = _collection.FindById(id);
            return doc?.StateVector;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _collection.DeleteAll();
            _logger?.LogInformation("Cleared all CRDTs from store");
        }
        finally
        {
            _lock.Release();
        }
    }

    private CrdtDocument SerializeCrdt(ICrdt crdt)
    {
        var type = crdt.GetType();
        var data = MessagePackSerializer.Serialize(type, crdt);

        return new CrdtDocument
        {
            Id = crdt.Id,
            TypeName = type.FullName ?? type.Name,
            Data = data,
            StateVector = crdt.GetStateVector(),
            LastModified = DateTimeOffset.UtcNow
        };
    }

    private T? DeserializeCrdt<T>(CrdtDocument doc) where T : class, ICrdt
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(doc.Data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize CRDT {Id} of type {Type}", doc.Id, doc.TypeName);
            return null;
        }
    }

    private object? DeserializeCrdt(CrdtDocument doc, Type type)
    {
        try
        {
            return MessagePackSerializer.Deserialize(type, doc.Data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize CRDT {Id} of type {Type}", doc.Id, doc.TypeName);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _lock.WaitAsync();
        try
        {
            _database.Dispose();
            _disposed = true;
            _logger?.LogInformation("LiteDB CRDT store disposed");
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}

/// <summary>
/// Document structure for storing CRDTs in LiteDB.
/// </summary>
internal sealed class CrdtDocument
{
    public Guid Id { get; set; }
    public string TypeName { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public byte[] StateVector { get; set; } = [];
    public DateTimeOffset LastModified { get; set; }
}
