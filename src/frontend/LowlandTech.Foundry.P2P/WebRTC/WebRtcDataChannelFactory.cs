using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using LowlandTech.Foundry.P2P.WebRTC.Signaling;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.WebRTC;

/// <summary>
/// Factory for creating WebRTC data channels.
/// </summary>
public sealed class WebRtcDataChannelFactory : IDataChannelFactory
{
    private readonly ISignalingChannel _signaling;
    private readonly IceConfiguration _iceConfig;
    private readonly ILogger<WebRtcDataChannelFactory>? _logger;

    public WebRtcDataChannelFactory(
        ISignalingChannel signaling,
        IceConfiguration? iceConfig = null,
        ILogger<WebRtcDataChannelFactory>? logger = null)
    {
        _signaling = signaling ?? throw new ArgumentNullException(nameof(signaling));
        _iceConfig = iceConfig ?? new IceConfiguration();
        _logger = logger;
    }

    public async Task<IDataChannel> CreateChannelAsync(
        PeerInfo remotePeer,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Creating WebRTC channel to peer {PeerId}", remotePeer.Id);

        return await WebRtcDataChannel.CreateAsOfferAsync(
            remotePeer,
            _signaling,
            _iceConfig,
            _logger,
            cancellationToken);
    }
}
