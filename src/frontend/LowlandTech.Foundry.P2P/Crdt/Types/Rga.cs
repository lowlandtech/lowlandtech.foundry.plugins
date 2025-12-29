using LowlandTech.Foundry.P2P.Identity;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Crdt.Types;

/// <summary>
/// Replicated Growable Array (RGA) - a CRDT for collaborative text editing.
/// Based on the RGA algorithm by Roh et al.
/// Supports insert and delete operations on a sequence of characters.
/// </summary>
public sealed class Rga : CrdtBase<string>
{
    private readonly SortedList<RgaNodeId, RgaNode> _nodes = new(new RgaNodeIdComparer());
    private readonly Dictionary<RgaNodeId, RgaNode> _nodeIndex = new();
    private long _operationCounter;

    public override string Value => GetText();

    /// <summary>
    /// Gets the length of the text.
    /// </summary>
    public int Length => _nodes.Values.Count(n => !n.IsDeleted);

    public Rga(Guid id, PeerId localPeerId) : base(id, localPeerId)
    {
        // Add a root node that serves as the anchor
        var rootId = new RgaNodeId { Timestamp = 0, PeerId = "" };
        var root = new RgaNode { Id = rootId, Character = '\0', IsDeleted = true };
        _nodes.Add(rootId, root);
        _nodeIndex.Add(rootId, root);
    }

    /// <summary>
    /// Gets the text content.
    /// </summary>
    public string GetText()
    {
        var chars = new List<char>();
        foreach (var node in _nodes.Values)
        {
            if (!node.IsDeleted && node.Character != '\0')
            {
                chars.Add(node.Character);
            }
        }
        return new string(chars.ToArray());
    }

    /// <summary>
    /// Inserts a character at the specified position.
    /// </summary>
    public void Insert(int position, char character)
    {
        var leftNode = GetNodeAtPosition(position - 1);
        var timestamp = Interlocked.Increment(ref _operationCounter);

        var newId = new RgaNodeId
        {
            Timestamp = timestamp,
            PeerId = LocalPeerId.ToBase58()
        };

        var newNode = new RgaNode
        {
            Id = newId,
            Character = character,
            IsDeleted = false,
            LeftParentId = leftNode?.Id ?? new RgaNodeId()
        };

        _nodes.Add(newId, newNode);
        _nodeIndex.Add(newId, newNode);

        IncrementClock();
        NotifyValueChanged();
    }

    /// <summary>
    /// Inserts a string at the specified position.
    /// </summary>
    public void Insert(int position, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            Insert(position + i, text[i]);
        }
    }

    /// <summary>
    /// Deletes the character at the specified position.
    /// </summary>
    public void Delete(int position)
    {
        var node = GetNodeAtPosition(position);
        if (node != null && !node.IsDeleted)
        {
            node.IsDeleted = true;
            IncrementClock();
            NotifyValueChanged();
        }
    }

    /// <summary>
    /// Deletes a range of characters.
    /// </summary>
    public void Delete(int position, int length)
    {
        for (var i = 0; i < length; i++)
        {
            Delete(position);
        }
    }

    private RgaNode? GetNodeAtPosition(int position)
    {
        if (position < 0)
            return _nodes.Values[0]; // Root node

        var currentPos = -1;
        foreach (var node in _nodes.Values)
        {
            if (!node.IsDeleted && node.Character != '\0')
            {
                currentPos++;
                if (currentPos == position)
                    return node;
            }
        }

        // Return the last visible node if position is at the end
        return _nodes.Values.LastOrDefault(n => !n.IsDeleted);
    }

    public override void Merge(ICrdt<string> other)
    {
        if (other is not Rga otherRga)
            throw new ArgumentException("Can only merge with another Rga");

        if (otherRga.Id != Id)
            throw new ArgumentException("Cannot merge RGAs with different IDs");

        var changed = false;

        foreach (var (nodeId, otherNode) in otherRga._nodeIndex)
        {
            if (_nodeIndex.TryGetValue(nodeId, out var existingNode))
            {
                // If the other node is deleted, mark ours as deleted too
                if (otherNode.IsDeleted && !existingNode.IsDeleted)
                {
                    existingNode.IsDeleted = true;
                    changed = true;
                }
            }
            else
            {
                // Add new node
                var newNode = new RgaNode
                {
                    Id = nodeId,
                    Character = otherNode.Character,
                    IsDeleted = otherNode.IsDeleted,
                    LeftParentId = otherNode.LeftParentId
                };

                _nodes.Add(nodeId, newNode);
                _nodeIndex.Add(nodeId, newNode);
                changed = true;

                // Update operation counter
                _operationCounter = Math.Max(_operationCounter, nodeId.Timestamp);
            }
        }

        Clock = Clock.Merge(otherRga.Clock);

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
        var state = new RgaState
        {
            Nodes = _nodeIndex.Values.Select(n => new RgaNodeState
            {
                Id = n.Id,
                Character = n.Character,
                IsDeleted = n.IsDeleted,
                LeftParentId = n.LeftParentId
            }).ToList(),
            Clock = Clock
        };
        return MessagePackSerializer.Serialize(state);
    }

    public override void ApplyDelta(byte[] delta)
    {
        var state = MessagePackSerializer.Deserialize<RgaState>(delta);
        var changed = false;

        foreach (var nodeState in state.Nodes)
        {
            if (_nodeIndex.TryGetValue(nodeState.Id, out var existingNode))
            {
                if (nodeState.IsDeleted && !existingNode.IsDeleted)
                {
                    existingNode.IsDeleted = true;
                    changed = true;
                }
            }
            else
            {
                var newNode = new RgaNode
                {
                    Id = nodeState.Id,
                    Character = nodeState.Character,
                    IsDeleted = nodeState.IsDeleted,
                    LeftParentId = nodeState.LeftParentId
                };

                _nodes.Add(nodeState.Id, newNode);
                _nodeIndex.Add(nodeState.Id, newNode);
                changed = true;

                _operationCounter = Math.Max(_operationCounter, nodeState.Id.Timestamp);
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
/// Unique identifier for RGA nodes, ordered by timestamp then peer ID.
/// </summary>
[MessagePackObject]
public sealed record RgaNodeId
{
    [Key(0)]
    public long Timestamp { get; set; }

    [Key(1)]
    public string PeerId { get; set; } = "";
}

/// <summary>
/// Comparer for RGA node IDs that determines the total order.
/// </summary>
internal sealed class RgaNodeIdComparer : IComparer<RgaNodeId>
{
    public int Compare(RgaNodeId? x, RgaNodeId? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var timestampCompare = x.Timestamp.CompareTo(y.Timestamp);
        if (timestampCompare != 0) return timestampCompare;

        return string.Compare(x.PeerId, y.PeerId, StringComparison.Ordinal);
    }
}

/// <summary>
/// A node in the RGA sequence.
/// </summary>
internal sealed class RgaNode
{
    public RgaNodeId Id { get; init; } = new();
    public char Character { get; init; }
    public bool IsDeleted { get; set; }
    public RgaNodeId LeftParentId { get; init; } = new();
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class RgaNodeState
{
    [Key(0)]
    public RgaNodeId Id { get; set; } = new();

    [Key(1)]
    public char Character { get; set; }

    [Key(2)]
    public bool IsDeleted { get; set; }

    [Key(3)]
    public RgaNodeId LeftParentId { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class RgaState
{
    [Key(0)]
    public List<RgaNodeState> Nodes { get; set; } = new();

    [Key(1)]
    public VectorClock Clock { get; set; } = new();
}
