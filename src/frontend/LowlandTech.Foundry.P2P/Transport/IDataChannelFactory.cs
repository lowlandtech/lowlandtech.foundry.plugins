using LowlandTech.Foundry.P2P.Identity;

namespace LowlandTech.Foundry.P2P.Transport;

/// <summary>
/// Factory for creating data channels to remote peers.
/// </summary>
public interface IDataChannelFactory
{
    /// <summary>
    /// Creates a data channel to a remote peer.
    /// </summary>
    Task<IDataChannel> CreateChannelAsync(PeerInfo remotePeer, CancellationToken cancellationToken = default);
}
