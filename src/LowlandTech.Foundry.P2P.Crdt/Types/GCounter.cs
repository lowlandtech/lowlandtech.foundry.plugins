using LowlandTech.Foundry.P2P.Core.Identity;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Grow-only Counter (G-Counter) - a counter that can only be incremented.
/// Each peer maintains its own count, and the total is the sum of all peer counts.
/// </summary>
public sealed class GCounter : CrdtBase<long>
{
    private readonly Dictionary<string, long> _counts = new();

    public override long Value => _counts.Values.Sum();

    /// <summary>
    /// Gets the count for a specific peer.
    /// </summary>
    public long GetPeerCount(PeerId peerId)
    {
        var key = peerId.ToBase58();
        return _counts.TryGetValue(key, out var count) ? count : 0;
    }

    public GCounter(Guid id, PeerId localPeerId) : base(id, localPeerId)
    {
    }

    /// <summary>
    /// Increments the counter by 1.
    /// </summary>
    public void Increment()
    {
        Increment(1);
    }

    /// <summary>
    /// Increments the counter by the specified amount.
    /// </summary>
    public void Increment(long amount)
    {
        if (amount < 0)
            throw new ArgumentException("G-Counter can only be incremented by positive values", nameof(amount));

        var key = LocalPeerId.ToBase58();
        _counts[key] = GetPeerCount(LocalPeerId) + amount;
        IncrementClock();
        NotifyValueChanged();
    }

    public override void Merge(ICrdt<long> other)
    {
        if (other is not GCounter otherCounter)
            throw new ArgumentException("Can only merge with another GCounter");

        if (otherCounter.Id != Id)
            throw new ArgumentException("Cannot merge counters with different IDs");

        var changed = false;

        foreach (var (key, value) in otherCounter._counts)
        {
            if (_counts.TryGetValue(key, out var existing))
            {
                if (value > existing)
                {
                    _counts[key] = value;
                    changed = true;
                }
            }
            else
            {
                _counts[key] = value;
                changed = true;
            }
        }

        Clock = Clock.Merge(otherCounter.Clock);

        if (changed)
        {
            NotifyValueChanged();
        }
    }

    public override byte[] GetStateVector()
    {
        return MessagePackSerializer.Serialize(Clock);
    }

    public override byte[] EncodeDelta(byte[] sinceStateVector)
    {
        var state = new GCounterState
        {
            Counts = new Dictionary<string, long>(_counts),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<GCounterState>(delta);
        var changed = false;

        foreach (var (key, value) in state.Counts)
        {
            if (_counts.TryGetValue(key, out var existing))
            {
                if (value > existing)
                {
                    _counts[key] = value;
                    changed = true;
                }
            }
            else
            {
                _counts[key] = value;
                changed = true;
            }
        }

        Clock = Clock.Merge(state.Clock);

        if (changed)
        {
            NotifyValueChanged();
        }
    }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class GCounterState
{
    [Key(0)]
    public Dictionary<string, long> Counts { get; set; } = new();

    [Key(1)]
    public VectorClock Clock { get; set; } = new();
}

/// <summary>
/// Positive-Negative Counter (PN-Counter) - a counter that supports both increment and decrement.
/// Implemented as two G-Counters: one for increments and one for decrements.
/// </summary>
public sealed class PnCounter : CrdtBase<long>
{
    private readonly GCounter _positive;
    private readonly GCounter _negative;

    public override long Value => _positive.Value - _negative.Value;

    public PnCounter(Guid id, PeerId localPeerId) : base(id, localPeerId)
    {
        _positive = new GCounter(id, localPeerId);
        _negative = new GCounter(id, localPeerId);

        _positive.ValueChanged += _ => NotifyValueChanged();
        _negative.ValueChanged += _ => NotifyValueChanged();
    }

    /// <summary>
    /// Increments the counter by 1.
    /// </summary>
    public void Increment()
    {
        Increment(1);
    }

    /// <summary>
    /// Increments the counter by the specified amount.
    /// </summary>
    public void Increment(long amount)
    {
        if (amount < 0)
            throw new ArgumentException("Use Decrement for negative values", nameof(amount));

        _positive.Increment(amount);
        IncrementClock();
    }

    /// <summary>
    /// Decrements the counter by 1.
    /// </summary>
    public void Decrement()
    {
        Decrement(1);
    }

    /// <summary>
    /// Decrements the counter by the specified amount.
    /// </summary>
    public void Decrement(long amount)
    {
        if (amount < 0)
            throw new ArgumentException("Use Increment for negative values", nameof(amount));

        _negative.Increment(amount);
        IncrementClock();
    }

    public override void Merge(ICrdt<long> other)
    {
        if (other is not PnCounter otherCounter)
            throw new ArgumentException("Can only merge with another PnCounter");

        if (otherCounter.Id != Id)
            throw new ArgumentException("Cannot merge counters with different IDs");

        _positive.Merge(otherCounter._positive);
        _negative.Merge(otherCounter._negative);
        Clock = Clock.Merge(otherCounter.Clock);
    }

    public override byte[] GetStateVector()
    {
        return MessagePackSerializer.Serialize(Clock);
    }

    public override byte[] EncodeDelta(byte[] sinceStateVector)
    {
        var state = new PnCounterState
        {
            PositiveDelta = _positive.EncodeDelta(sinceStateVector),
            NegativeDelta = _negative.EncodeDelta(sinceStateVector),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<PnCounterState>(delta);

        _positive.ApplyDelta(state.PositiveDelta);
        _negative.ApplyDelta(state.NegativeDelta);
        Clock = Clock.Merge(state.Clock);
    }
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class PnCounterState
{
    [Key(0)]
    public byte[] PositiveDelta { get; set; } = [];

    [Key(1)]
    public byte[] NegativeDelta { get; set; } = [];

    [Key(2)]
    public VectorClock Clock { get; set; } = new();
}
