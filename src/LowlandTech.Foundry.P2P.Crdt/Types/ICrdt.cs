using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Non-generic base interface for all CRDTs.
/// Used for storage and sync operations that don't need to know the specific state type.
/// </summary>
public interface ICrdt
{
    /// <summary>
    /// Unique identifier for this CRDT instance.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Vector clock tracking causality across peers.
    /// </summary>
    VectorClock Clock { get; }

    /// <summary>
    /// Gets the current state vector for delta sync.
    /// </summary>
    byte[] GetStateVector();

    /// <summary>
    /// Encodes changes since the given state vector as a delta.
    /// </summary>
    byte[] EncodeDelta(byte[] sinceStateVector);

    /// <summary>
    /// Applies a delta received from another peer.
    /// </summary>
    void ApplyDelta(byte[] delta);
}

/// <summary>
/// Base interface for all CRDT (Conflict-free Replicated Data Type) implementations.
/// CRDTs allow concurrent updates without coordination and guarantee eventual consistency.
/// </summary>
/// <typeparam name="TState">The type of the CRDT's value/state.</typeparam>
public interface ICrdt<TState> : ICrdt
{
    /// <summary>
    /// The current value/state of the CRDT.
    /// </summary>
    TState Value { get; }

    /// <summary>
    /// Merges another CRDT state into this one.
    /// This operation must be commutative, associative, and idempotent.
    /// </summary>
    void Merge(ICrdt<TState> other);

    /// <summary>
    /// Raised when the CRDT value changes.
    /// </summary>
    event Action<TState>? ValueChanged;
}

/// <summary>
/// Base class for CRDT implementations providing common functionality.
/// </summary>
/// <typeparam name="TState">The type of the CRDT's value/state.</typeparam>
public abstract class CrdtBase<TState> : ICrdt<TState>
{
    public Guid Id { get; }
    public VectorClock Clock { get; protected set; }
    public abstract TState Value { get; }

    public event Action<TState>? ValueChanged;

    protected PeerId LocalPeerId { get; }

    protected CrdtBase(Guid id, PeerId localPeerId)
    {
        Id = id;
        LocalPeerId = localPeerId;
        Clock = new VectorClock();
    }

    protected CrdtBase(Guid id, PeerId localPeerId, VectorClock clock)
    {
        Id = id;
        LocalPeerId = localPeerId;
        Clock = clock;
    }

    public abstract void Merge(ICrdt<TState> other);
    public abstract byte[] GetStateVector();
    public abstract byte[] EncodeDelta(byte[] sinceStateVector);
    public abstract void ApplyDelta(byte[] delta);

    protected void IncrementClock()
    {
        Clock = Clock.Increment(LocalPeerId);
    }

    protected void NotifyValueChanged()
    {
        ValueChanged?.Invoke(Value);
    }
}
