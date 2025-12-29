using System.Collections.Frozen;
using LowlandTech.Foundry.P2P.Identity;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Vector clock for tracking causality across distributed peers.
/// Each peer maintains its own logical timestamp, and the clock tracks all peers' timestamps.
/// </summary>
[MessagePackObject]
public sealed class VectorClock : IEquatable<VectorClock>
{
    [Key(0)]
    public Dictionary<string, long> Entries { get; init; }

    public VectorClock()
    {
        Entries = new Dictionary<string, long>();
    }

    [SerializationConstructor]
    public VectorClock(Dictionary<string, long> entries)
    {
        Entries = entries ?? new Dictionary<string, long>();
    }

    /// <summary>
    /// Gets the timestamp for a specific peer.
    /// </summary>
    public long Get(PeerId peerId)
    {
        var key = peerId.ToBase58();
        return Entries.TryGetValue(key, out var value) ? value : 0;
    }

    /// <summary>
    /// Creates a new clock with the specified peer's timestamp incremented.
    /// </summary>
    public VectorClock Increment(PeerId peerId)
    {
        var key = peerId.ToBase58();
        var newEntries = new Dictionary<string, long>(Entries);
        newEntries[key] = Get(peerId) + 1;
        return new VectorClock(newEntries);
    }

    /// <summary>
    /// Sets the timestamp for a specific peer.
    /// </summary>
    public VectorClock Set(PeerId peerId, long timestamp)
    {
        var key = peerId.ToBase58();
        var newEntries = new Dictionary<string, long>(Entries);
        newEntries[key] = timestamp;
        return new VectorClock(newEntries);
    }

    /// <summary>
    /// Merges two vector clocks, taking the maximum of each peer's timestamp.
    /// </summary>
    public VectorClock Merge(VectorClock other)
    {
        var newEntries = new Dictionary<string, long>(Entries);

        foreach (var (key, value) in other.Entries)
        {
            if (newEntries.TryGetValue(key, out var existing))
            {
                newEntries[key] = Math.Max(existing, value);
            }
            else
            {
                newEntries[key] = value;
            }
        }

        return new VectorClock(newEntries);
    }

    /// <summary>
    /// Determines if this clock happened before another clock.
    /// Returns true if all entries in this clock are less than or equal to
    /// the corresponding entries in the other clock, and at least one is strictly less.
    /// </summary>
    public bool HappensBefore(VectorClock other)
    {
        var allLessOrEqual = true;
        var atLeastOneLess = false;

        var allKeys = Entries.Keys.Union(other.Entries.Keys);

        foreach (var key in allKeys)
        {
            var thisValue = Entries.TryGetValue(key, out var v1) ? v1 : 0;
            var otherValue = other.Entries.TryGetValue(key, out var v2) ? v2 : 0;

            if (thisValue > otherValue)
            {
                allLessOrEqual = false;
                break;
            }

            if (thisValue < otherValue)
            {
                atLeastOneLess = true;
            }
        }

        return allLessOrEqual && atLeastOneLess;
    }

    /// <summary>
    /// Determines if two clocks are concurrent (neither happened before the other).
    /// </summary>
    public bool IsConcurrent(VectorClock other)
    {
        return !HappensBefore(other) && !other.HappensBefore(this) && !Equals(other);
    }

    /// <summary>
    /// Returns the sum of all timestamps (useful for comparison).
    /// </summary>
    public long Sum() => Entries.Values.Sum();

    public bool Equals(VectorClock? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Entries.Count != other.Entries.Count) return false;

        foreach (var (key, value) in Entries)
        {
            if (!other.Entries.TryGetValue(key, out var otherValue) || value != otherValue)
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as VectorClock);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in Entries.OrderBy(e => e.Key))
        {
            hash.Add(key);
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var entries = string.Join(", ", Entries.Select(e => $"{e.Key[..8]}:{e.Value}"));
        return $"VectorClock({entries})";
    }

    public static bool operator ==(VectorClock? left, VectorClock? right)
        => Equals(left, right);

    public static bool operator !=(VectorClock? left, VectorClock? right)
        => !Equals(left, right);
}

/// <summary>
/// Hybrid Logical Clock combining physical and logical time.
/// Provides globally unique, monotonically increasing timestamps.
/// </summary>
[MessagePackObject]
public sealed record HybridLogicalClock : IComparable<HybridLogicalClock>
{
    [Key(0)]
    public long WallTime { get; init; }

    [Key(1)]
    public int Counter { get; init; }

    [Key(2)]
    public string PeerId { get; init; }

    public HybridLogicalClock() : this(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0, "") { }

    [SerializationConstructor]
    public HybridLogicalClock(long wallTime, int counter, string peerId)
    {
        WallTime = wallTime;
        Counter = counter;
        PeerId = peerId;
    }

    /// <summary>
    /// Creates a new HLC for the local peer.
    /// </summary>
    public static HybridLogicalClock Now(PeerId peerId)
    {
        return new HybridLogicalClock(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            0,
            peerId.ToBase58());
    }

    /// <summary>
    /// Advances the clock for a local event.
    /// </summary>
    public HybridLogicalClock Tick(PeerId peerId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var newWallTime = Math.Max(now, WallTime);
        var newCounter = newWallTime == WallTime ? Counter + 1 : 0;

        return new HybridLogicalClock(newWallTime, newCounter, peerId.ToBase58());
    }

    /// <summary>
    /// Updates the clock based on a received message's timestamp.
    /// </summary>
    public HybridLogicalClock Receive(HybridLogicalClock received, PeerId localPeerId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var maxWallTime = Math.Max(now, Math.Max(WallTime, received.WallTime));

        int newCounter;
        if (maxWallTime == WallTime && maxWallTime == received.WallTime)
        {
            newCounter = Math.Max(Counter, received.Counter) + 1;
        }
        else if (maxWallTime == WallTime)
        {
            newCounter = Counter + 1;
        }
        else if (maxWallTime == received.WallTime)
        {
            newCounter = received.Counter + 1;
        }
        else
        {
            newCounter = 0;
        }

        return new HybridLogicalClock(maxWallTime, newCounter, localPeerId.ToBase58());
    }

    public int CompareTo(HybridLogicalClock? other)
    {
        if (other is null) return 1;

        var wallCompare = WallTime.CompareTo(other.WallTime);
        if (wallCompare != 0) return wallCompare;

        var counterCompare = Counter.CompareTo(other.Counter);
        if (counterCompare != 0) return counterCompare;

        return string.Compare(PeerId, other.PeerId, StringComparison.Ordinal);
    }

    public static bool operator <(HybridLogicalClock left, HybridLogicalClock right)
        => left.CompareTo(right) < 0;

    public static bool operator >(HybridLogicalClock left, HybridLogicalClock right)
        => left.CompareTo(right) > 0;

    public static bool operator <=(HybridLogicalClock left, HybridLogicalClock right)
        => left.CompareTo(right) <= 0;

    public static bool operator >=(HybridLogicalClock left, HybridLogicalClock right)
        => left.CompareTo(right) >= 0;

    /// <summary>
    /// Converts the wall time to a DateTimeOffset.
    /// </summary>
    public DateTimeOffset ToDateTimeOffset()
        => DateTimeOffset.FromUnixTimeMilliseconds(WallTime);

    /// <summary>
    /// Gets the local DateTime representation.
    /// </summary>
    [IgnoreMember]
    public DateTime LocalDateTime
        => ToDateTimeOffset().LocalDateTime;
}
