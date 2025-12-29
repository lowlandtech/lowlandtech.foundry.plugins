using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using LowlandTech.Foundry.P2P.WebRTC.Signaling;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;

namespace LowlandTech.Foundry.P2P.WebRTC;

/// <summary>
/// WebRTC-based data channel implementation using SIPSorcery.
/// </summary>
public sealed class WebRtcDataChannel : IDataChannel
{
    private readonly RTCPeerConnection _peerConnection;
    private readonly RTCDataChannel _dataChannel;
    private readonly ILogger? _logger;
    private bool _disposed;

    public DataChannelState State { get; private set; }

    public event Func<ReadOnlyMemory<byte>, Task>? MessageReceived;
    public event Func<DataChannelState, Task>? StateChanged;

    private WebRtcDataChannel(
        RTCPeerConnection peerConnection,
        RTCDataChannel dataChannel,
        ILogger? logger)
    {
        _peerConnection = peerConnection;
        _dataChannel = dataChannel;
        _logger = logger;

        SetupHandlers();
    }

    private void SetupHandlers()
    {
        _dataChannel.onopen += () =>
        {
            _logger?.LogDebug("Data channel opened");
            UpdateState(DataChannelState.Open);
        };

        _dataChannel.onclose += () =>
        {
            _logger?.LogDebug("Data channel closed");
            UpdateState(DataChannelState.Closed);
        };

        _dataChannel.onerror += error =>
        {
            _logger?.LogError("Data channel error: {Error}", error);
            UpdateState(DataChannelState.Failed);
        };

        _dataChannel.onmessage += (channel, protocol, data) =>
        {
            MessageReceived?.Invoke(data);
        };

        _peerConnection.onconnectionstatechange += state =>
        {
            _logger?.LogDebug("Connection state changed: {State}", state);

            if (state == RTCPeerConnectionState.failed)
                UpdateState(DataChannelState.Failed);
            else if (state == RTCPeerConnectionState.closed)
                UpdateState(DataChannelState.Closed);
        };
    }

    private void UpdateState(DataChannelState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Creates a data channel as the offering peer (initiator).
    /// </summary>
    public static async Task<WebRtcDataChannel> CreateAsOfferAsync(
        PeerInfo remotePeer,
        ISignalingChannel signaling,
        IceConfiguration iceConfig,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var config = CreateRtcConfiguration(iceConfig);
        var peerConnection = new RTCPeerConnection(config);

        var channelInit = new RTCDataChannelInit { ordered = true };
        var dataChannel = await peerConnection.createDataChannel("data", channelInit);

        var webRtcChannel = new WebRtcDataChannel(peerConnection, dataChannel, logger);
        webRtcChannel.State = DataChannelState.Connecting;

        // Set up ICE candidate handling
        peerConnection.onicecandidate += async candidate =>
        {
            if (candidate != null)
            {
                await signaling.SendIceCandidateAsync(
                    remotePeer.Id,
                    candidate.candidate,
                    candidate.sdpMid,
                    candidate.sdpMLineIndex);
            }
        };

        // Handle incoming signaling messages
        var answerReceived = new TaskCompletionSource<string>();
        var iceCandidates = new List<RTCIceCandidateInit>();

        void HandleSignaling(SignalingMessage message)
        {
            if (message.FromPeerId != remotePeer.Id) return;

            if (message is AnswerMessage answer)
            {
                answerReceived.TrySetResult(answer.Sdp);
            }
            else if (message is IceCandidateMessage ice)
            {
                iceCandidates.Add(new RTCIceCandidateInit
                {
                    candidate = ice.Candidate,
                    sdpMid = ice.SdpMid,
                    sdpMLineIndex = (ushort)(ice.SdpMLineIndex ?? 0)
                });
            }
        }

        signaling.MessageReceived += msg => { HandleSignaling(msg); return Task.CompletedTask; };

        // Create and send offer
        var offer = peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);
        await signaling.SendOfferAsync(remotePeer.Id, offer.sdp);

        // Wait for answer
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(iceConfig.ConnectionTimeoutMs);

        try
        {
            var answerSdp = await answerReceived.Task.WaitAsync(cts.Token);
            var answerDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = answerSdp
            };
            peerConnection.setRemoteDescription(answerDesc);

            // Add any ICE candidates that arrived
            foreach (var candidate in iceCandidates)
            {
                peerConnection.addIceCandidate(candidate);
            }

            // Wait for connection to establish
            var connectionComplete = new TaskCompletionSource<bool>();
            peerConnection.onconnectionstatechange += state =>
            {
                if (state == RTCPeerConnectionState.connected)
                    connectionComplete.TrySetResult(true);
                else if (state == RTCPeerConnectionState.failed)
                    connectionComplete.TrySetException(new Exception("WebRTC connection failed"));
            };

            await connectionComplete.Task.WaitAsync(cts.Token);

            return webRtcChannel;
        }
        catch
        {
            peerConnection.close();
            throw;
        }
    }

    /// <summary>
    /// Creates a data channel as the answering peer.
    /// </summary>
    public static async Task<WebRtcDataChannel> CreateAsAnswerAsync(
        PeerId remotePeerId,
        string offerSdp,
        ISignalingChannel signaling,
        IceConfiguration iceConfig,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var config = CreateRtcConfiguration(iceConfig);
        var peerConnection = new RTCPeerConnection(config);

        RTCDataChannel? dataChannel = null;
        var dataChannelOpened = new TaskCompletionSource<RTCDataChannel>();

        // Wait for the data channel from the remote peer
        peerConnection.ondatachannel += channel =>
        {
            dataChannel = channel;
            dataChannelOpened.TrySetResult(channel);
        };

        // Set up ICE candidate handling
        peerConnection.onicecandidate += async candidate =>
        {
            if (candidate != null)
            {
                await signaling.SendIceCandidateAsync(
                    remotePeerId,
                    candidate.candidate,
                    candidate.sdpMid,
                    candidate.sdpMLineIndex);
            }
        };

        // Handle incoming ICE candidates
        signaling.MessageReceived += msg =>
        {
            if (msg.FromPeerId != remotePeerId) return Task.CompletedTask;

            if (msg is IceCandidateMessage ice)
            {
                peerConnection.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = ice.Candidate,
                    sdpMid = ice.SdpMid,
                    sdpMLineIndex = (ushort)(ice.SdpMLineIndex ?? 0)
                });
            }

            return Task.CompletedTask;
        };

        try
        {
            // Set remote description (the offer)
            var offerDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offerSdp
            };
            peerConnection.setRemoteDescription(offerDesc);

            // Create and send answer
            var answer = peerConnection.createAnswer();
            await peerConnection.setLocalDescription(answer);
            await signaling.SendAnswerAsync(remotePeerId, answer.sdp);

            // Wait for data channel
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(iceConfig.ConnectionTimeoutMs);

            dataChannel = await dataChannelOpened.Task.WaitAsync(cts.Token);

            var webRtcChannel = new WebRtcDataChannel(peerConnection, dataChannel, logger);
            webRtcChannel.State = DataChannelState.Connecting;

            // Wait for connection to establish
            var connectionComplete = new TaskCompletionSource<bool>();
            peerConnection.onconnectionstatechange += state =>
            {
                if (state == RTCPeerConnectionState.connected)
                    connectionComplete.TrySetResult(true);
                else if (state == RTCPeerConnectionState.failed)
                    connectionComplete.TrySetException(new Exception("WebRTC connection failed"));
            };

            await connectionComplete.Task.WaitAsync(cts.Token);

            return webRtcChannel;
        }
        catch
        {
            peerConnection.close();
            throw;
        }
    }

    private static RTCConfiguration CreateRtcConfiguration(IceConfiguration iceConfig)
    {
        var iceServers = new List<RTCIceServer>();

        foreach (var stun in iceConfig.StunServers)
        {
            iceServers.Add(new RTCIceServer { urls = stun.Uri });
        }

        foreach (var turn in iceConfig.TurnServers)
        {
            iceServers.Add(new RTCIceServer
            {
                urls = turn.Uri,
                username = turn.Username,
                credential = turn.Credential
            });
        }

        return new RTCConfiguration
        {
            iceServers = iceServers,
            iceTransportPolicy = iceConfig.TransportPolicy == IceTransportPolicy.Relay
                ? RTCIceTransportPolicy.relay
                : RTCIceTransportPolicy.all
        };
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (State != DataChannelState.Open)
            throw new InvalidOperationException($"Cannot send: channel state is {State}");

        _dataChannel.send(data.ToArray());
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _dataChannel.close();
        _peerConnection.close();

        await Task.CompletedTask;
    }
}
