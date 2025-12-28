using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Core.Transport;

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
