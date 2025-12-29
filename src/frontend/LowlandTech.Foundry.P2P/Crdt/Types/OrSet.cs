using LowlandTech.Foundry.P2P.Identity;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Observed-Remove Set (OR-Set) - a set that supports both add and remove operations.
/// Each element is tagged with a unique identifier to distinguish different "adds".
/// Remove only affects the tags that were observed at the time of removal.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
public sealed class OrSet<T> : CrdtBase<IReadOnlySet<T>> where T : notnull
{
    private readonly Dictionary<T, HashSet<UniqueTag>> _elements = new();
    private readonly HashSet<UniqueTag> _tombstones = new();
    private long _tagCounter;

    public override IReadOnlySet<T> Value
    {
        get
        {
            var result = new HashSet<T>();
            foreach (var (element, tags) in _elements)
            {
                if (tags.Any(t => !_tombstones.Contains(t)))
                {
                    result.Add(element);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the count of elements in the set.
    /// </summary>
    public int Count => Value.Count;

    public OrSet(Guid id, PeerId localPeerId) : base(id, localPeerId)
    {
    }

    /// <summary>
    /// Checks if the set contains the specified element.
    /// </summary>
    public bool Contains(T element)
    {
        if (!_elements.TryGetValue(element, out var tags))
            return false;

        return tags.Any(t => !_tombstones.Contains(t));
    }

    /// <summary>
    /// Adds an element to the set.
    /// </summary>
    public void Add(T element)
    {
        var tag = CreateTag();

        if (!_elements.TryGetValue(element, out var tags))
        {
            tags = new HashSet<UniqueTag>();
            _elements[element] = tags;
        }

        tags.Add(tag);
        IncrementClock();
        NotifyValueChanged();
    }

    /// <summary>
    /// Removes an element from the set.
    /// Only removes the currently observed tags for this element.
    /// </summary>
    public bool Remove(T element)
    {
        if (!_elements.TryGetValue(element, out var tags))
            return false;

        var removed = false;
        foreach (var tag in tags)
        {
            if (!_tombstones.Contains(tag))
            {
                _tombstones.Add(tag);
                removed = true;
            }
        }

        if (removed)
        {
            IncrementClock();
            NotifyValueChanged();
        }

        return removed;
    }

    /// <summary>
    /// Clears all elements from the set.
    /// </summary>
    public void Clear()
    {
        var changed = false;

        foreach (var (_, tags) in _elements)
        {
            foreach (var tag in tags)
            {
                if (!_tombstones.Contains(tag))
                {
                    _tombstones.Add(tag);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            IncrementClock();
            NotifyValueChanged();
        }
    }

    private UniqueTag CreateTag()
    {
        return new UniqueTag
        {
            PeerId = LocalPeerId.ToBase58(),
            Counter = Interlocked.Increment(ref _tagCounter)
        };
    }

    public override void Merge(ICrdt<IReadOnlySet<T>> other)
    {
        if (other is not OrSet<T> otherSet)
            throw new ArgumentException("Can only merge with another OrSet of the same type");

        if (otherSet.Id != Id)
            throw new ArgumentException("Cannot merge sets with different IDs");

        var changed = false;

        // Merge elements and their tags
        foreach (var (element, otherTags) in otherSet._elements)
        {
            if (!_elements.TryGetValue(element, out var tags))
            {
                tags = new HashSet<UniqueTag>();
                _elements[element] = tags;
            }

            foreach (var tag in otherTags)
            {
                if (tags.Add(tag))
                {
                    changed = true;
                }
            }
        }

        // Merge tombstones
        foreach (var tombstone in otherSet._tombstones)
        {
            if (_tombstones.Add(tombstone))
            {
                changed = true;
            }
        }

        Clock = Clock.Merge(otherSet.Clock);

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
        // Serialize full state for simplicity
        // A more efficient implementation would track and send only changes
        var state = new OrSetState<T>
        {
            Elements = _elements.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()),
            Tombstones = _tombstones.ToList(),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<OrSetState<T>>(delta);
        var changed = false;

        foreach (var (element, otherTags) in state.Elements)
        {
            if (!_elements.TryGetValue(element, out var tags))
            {
                tags = new HashSet<UniqueTag>();
                _elements[element] = tags;
            }

            foreach (var tag in otherTags)
            {
                if (tags.Add(tag))
                {
                    changed = true;
                }
            }
        }

        foreach (var tombstone in state.Tombstones)
        {
            if (_tombstones.Add(tombstone))
            {
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

/// <summary>
/// Unique tag for OR-Set elements, combining peer ID and a local counter.
/// </summary>
[MessagePackObject]
public sealed record UniqueTag
{
    [Key(0)]
    public string PeerId { get; set; } = "";

    [Key(1)]
    public long Counter { get; set; }

    public override int GetHashCode() => HashCode.Combine(PeerId, Counter);
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class OrSetState<T> where T : notnull
{
    [Key(0)]
    public Dictionary<T, List<UniqueTag>> Elements { get; set; } = new();

    [Key(1)]
    public List<UniqueTag> Tombstones { get; set; } = new();

    [Key(2)]
    public VectorClock Clock { get; set; } = new();
}
