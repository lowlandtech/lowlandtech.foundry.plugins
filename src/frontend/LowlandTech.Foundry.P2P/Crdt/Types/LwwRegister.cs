using LowlandTech.Foundry.P2P.Identity;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Last-Writer-Wins Register - stores a single value with timestamp-based conflict resolution.
/// When concurrent writes occur, the one with the higher timestamp wins.
/// </summary>
/// <typeparam name="T">The type of value stored in the register.</typeparam>
public sealed class LwwRegister<T> : CrdtBase<T?>
{
    private T? _value;
    private HybridLogicalClock _timestamp;

    public override T? Value => _value;

    /// <summary>
    /// The timestamp of the last write.
    /// </summary>
    public HybridLogicalClock Timestamp => _timestamp;

    public LwwRegister(Guid id, PeerId localPeerId)
        : base(id, localPeerId)
    {
        _value = default;
        _timestamp = HybridLogicalClock.Now(localPeerId);
    }

    public LwwRegister(Guid id, PeerId localPeerId, T? initialValue)
        : base(id, localPeerId)
    {
        _value = initialValue;
        _timestamp = HybridLogicalClock.Now(localPeerId);
    }

    /// <summary>
    /// Sets the register's value.
    /// </summary>
    public void Set(T? value)
    {
        _timestamp = _timestamp.Tick(LocalPeerId);
        _value = value;
        IncrementClock();
        NotifyValueChanged();
    }

    public override void Merge(ICrdt<T?> other)
    {
        if (other is not LwwRegister<T> otherRegister)
            throw new ArgumentException("Can only merge with another LwwRegister of the same type");

        if (otherRegister.Id != Id)
            throw new ArgumentException("Cannot merge registers with different IDs");

        // Last-writer-wins: take the value with the higher timestamp
        if (otherRegister.Timestamp > _timestamp)
        {
            _value = otherRegister.Value;
            _timestamp = otherRegister.Timestamp;
            NotifyValueChanged();
        }

        Clock = Clock.Merge(otherRegister.Clock);
    }

    public override byte[] GetStateVector()
    {
        return MessagePackSerializer.Serialize(Clock);
    }

    public override byte[] EncodeDelta(byte[] sinceStateVector)
    {
        // For LWW Register, we always send the full state as delta
        var state = new LwwRegisterState<T>
        {
            Value = _value,
            Timestamp = _timestamp,
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<LwwRegisterState<T>>(delta);

        if (state.Timestamp > _timestamp)
        {
            _value = state.Value;
            _timestamp = state.Timestamp;
            NotifyValueChanged();
        }

        Clock = Clock.Merge(state.Clock);
    }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class LwwRegisterState<T>
{
    [Key(0)]
    public T? Value { get; set; }

    [Key(1)]
    public HybridLogicalClock Timestamp { get; set; } = new();

    [Key(2)]
    public VectorClock Clock { get; set; } = new();
}
