namespace LowlandTech.Foundry.P2P.Transport;

/// <summary>
/// Represents a bidirectional data channel for peer communication.
/// </summary>
public interface IDataChannel : IAsyncDisposable
{
    /// <summary>
    /// Current state of the data channel.
    /// </summary>
    DataChannelState State { get; }

    /// <summary>
    /// Raised when a message is received.
    /// </summary>
    event Func<ReadOnlyMemory<byte>, Task>? MessageReceived;

    /// <summary>
    /// Raised when the channel state changes.
    /// </summary>
    event Func<DataChannelState, Task>? StateChanged;

    /// <summary>
    /// Sends data through the channel.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

/// <summary>
/// State of a data channel.
/// </summary>
public enum DataChannelState
{
    /// <summary>Channel is being established.</summary>
    Connecting,

    /// <summary>Channel is open and ready for data.</summary>
    Open,

    /// <summary>Channel is closing.</summary>
    Closing,

    /// <summary>Channel is closed.</summary>
    Closed,

    /// <summary>Channel failed.</summary>
    Failed
}
