using LowlandTech.Foundry.P2P.Crdt.Types;

namespace LowlandTech.Foundry.P2P.Crdt.Storage;

/// <summary>
/// Interface for persisting CRDTs to local storage.
/// </summary>
public interface ICrdtStore : IAsyncDisposable
{
    /// <summary>
    /// Gets a CRDT by its ID (non-generic version).
    /// </summary>
    /// <param name="id">The CRDT's unique identifier.</param>
    /// <returns>The CRDT if found, null otherwise.</returns>
    Task<ICrdt?> GetAsync(Guid id);

    /// <summary>
    /// Gets a CRDT by its ID with a specific type.
    /// </summary>
    /// <typeparam name="T">The CRDT type.</typeparam>
    /// <param name="id">The CRDT's unique identifier.</param>
    /// <returns>The CRDT if found and matches the type, null otherwise.</returns>
    Task<T?> GetAsync<T>(Guid id) where T : class, ICrdt;

    /// <summary>
    /// Gets all CRDTs of a specific type.
    /// </summary>
    /// <typeparam name="T">The CRDT type.</typeparam>
    /// <returns>All CRDTs of the specified type.</returns>
    Task<IReadOnlyList<T>> GetAllAsync<T>() where T : class, ICrdt;

    /// <summary>
    /// Saves a CRDT to storage.
    /// </summary>
    /// <param name="crdt">The CRDT to save.</param>
    Task SaveAsync(ICrdt crdt);

    /// <summary>
    /// Deletes a CRDT from storage.
    /// </summary>
    /// <param name="id">The CRDT's unique identifier.</param>
    /// <returns>True if the CRDT was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Checks if a CRDT exists in storage.
    /// </summary>
    /// <param name="id">The CRDT's unique identifier.</param>
    /// <returns>True if the CRDT exists.</returns>
    Task<bool> ExistsAsync(Guid id);

    /// <summary>
    /// Gets all CRDT IDs in storage.
    /// </summary>
    /// <returns>All CRDT IDs.</returns>
    Task<IReadOnlyList<Guid>> GetAllIdsAsync();

    /// <summary>
    /// Gets the state vector for a specific CRDT.
    /// </summary>
    /// <param name="id">The CRDT's unique identifier.</param>
    /// <returns>The state vector bytes if found, null otherwise.</returns>
    Task<byte[]?> GetStateVectorAsync(Guid id);

    /// <summary>
    /// Clears all data from the store.
    /// </summary>
    Task ClearAsync();
}
