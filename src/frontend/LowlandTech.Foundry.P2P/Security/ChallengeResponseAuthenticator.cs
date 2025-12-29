using System.Security.Cryptography;
using LowlandTech.Foundry.P2P.Identity;
using LowlandTech.Foundry.P2P.Transport;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Security;

/// <summary>
/// Challenge-response authenticator for verifying peer identity.
///
/// Protocol:
/// 1. A -> B: Hello(PeerId_A, PublicKey_A)
/// 2. B -> A: Challenge(nonce)
/// 3. A -> B: Response(Sign(nonce, PrivateKey_A))
/// 4. B verifies signature using PublicKey_A
/// 5. Repeat in reverse direction for mutual authentication
/// </summary>
public sealed class ChallengeResponseAuthenticator : IPeerAuthenticator
{
    private const int NonceSize = 32;
    private const int TimeoutMs = 30000;

    private readonly ILogger<ChallengeResponseAuthenticator>? _logger;

    public ChallengeResponseAuthenticator(ILogger<ChallengeResponseAuthenticator>? logger = null)
    {
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        PeerConnection connection,
        PeerIdentity localIdentity,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeoutMs);

        try
        {
            _logger?.LogDebug("Starting authentication with peer {PeerId}", connection.RemotePeerId);

            // Step 1: Send Hello
            var hello = new AuthHello
            {
                PeerId = localIdentity.Id.ToBase58(),
                PublicKey = localIdentity.PublicKey
            };

            await SendMessageAsync(connection, AuthMessageType.Hello, hello, cts.Token);

            // Step 2: Receive Challenge
            var (challengeMsgType, challengeData) = await ReceiveMessageAsync(connection, cts.Token);

            if (challengeMsgType != AuthMessageType.Challenge)
            {
                return AuthenticationResult.Failed($"Expected Challenge, got {challengeMsgType}");
            }

            var challenge = MessagePackSerializer.Deserialize<AuthChallenge>(challengeData);

            // Step 3: Send Response (sign the nonce)
            var signature = localIdentity.Sign(challenge.Nonce);
            var response = new AuthResponse { Signature = signature };

            await SendMessageAsync(connection, AuthMessageType.Response, response, cts.Token);

            // Step 4: Receive their Hello (for mutual authentication)
            var (theirHelloType, theirHelloData) = await ReceiveMessageAsync(connection, cts.Token);

            if (theirHelloType != AuthMessageType.Hello)
            {
                return AuthenticationResult.Failed($"Expected Hello, got {theirHelloType}");
            }

            var theirHello = MessagePackSerializer.Deserialize<AuthHello>(theirHelloData);

            // Verify their identity matches the connection
            if (!VerifyPeerIdentity(connection.RemotePeerId, theirHello.PublicKey))
            {
                return AuthenticationResult.Failed("Peer identity mismatch");
            }

            // Step 5: Send Challenge for mutual authentication
            var ourNonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ourChallenge = new AuthChallenge { Nonce = ourNonce };

            await SendMessageAsync(connection, AuthMessageType.Challenge, ourChallenge, cts.Token);

            // Step 6: Receive their Response
            var (theirResponseType, theirResponseData) = await ReceiveMessageAsync(connection, cts.Token);

            if (theirResponseType != AuthMessageType.Response)
            {
                return AuthenticationResult.Failed($"Expected Response, got {theirResponseType}");
            }

            var theirResponse = MessagePackSerializer.Deserialize<AuthResponse>(theirResponseData);

            // Verify their signature
            if (!PeerIdentity.Verify(theirHello.PublicKey, ourNonce, theirResponse.Signature))
            {
                return AuthenticationResult.Failed("Invalid signature from peer");
            }

            // Send Success
            await SendMessageAsync(connection, AuthMessageType.Success, new AuthSuccess(), cts.Token);

            // Wait for their Success
            var (successType, _) = await ReceiveMessageAsync(connection, cts.Token);

            if (successType != AuthMessageType.Success)
            {
                return AuthenticationResult.Failed($"Expected Success, got {successType}");
            }

            _logger?.LogInformation("Successfully authenticated peer {PeerId}", connection.RemotePeerId);

            return AuthenticationResult.Succeeded(
                PeerId.Parse(theirHello.PeerId),
                theirHello.PublicKey);
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failed("Authentication timed out");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Authentication failed");
            return AuthenticationResult.Failed($"Authentication error: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> HandleAuthenticationAsync(
        PeerConnection connection,
        PeerIdentity localIdentity,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeoutMs);

        try
        {
            _logger?.LogDebug("Handling authentication from peer {PeerId}", connection.RemotePeerId);

            // Step 1: Receive Hello
            var (helloType, helloData) = await ReceiveMessageAsync(connection, cts.Token);

            if (helloType != AuthMessageType.Hello)
            {
                return AuthenticationResult.Failed($"Expected Hello, got {helloType}");
            }

            var theirHello = MessagePackSerializer.Deserialize<AuthHello>(helloData);

            // Verify identity matches
            if (!VerifyPeerIdentity(connection.RemotePeerId, theirHello.PublicKey))
            {
                return AuthenticationResult.Failed("Peer identity mismatch");
            }

            // Step 2: Send Challenge
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var challenge = new AuthChallenge { Nonce = nonce };

            await SendMessageAsync(connection, AuthMessageType.Challenge, challenge, cts.Token);

            // Step 3: Receive Response
            var (responseType, responseData) = await ReceiveMessageAsync(connection, cts.Token);

            if (responseType != AuthMessageType.Response)
            {
                return AuthenticationResult.Failed($"Expected Response, got {responseType}");
            }

            var response = MessagePackSerializer.Deserialize<AuthResponse>(responseData);

            // Verify signature
            if (!PeerIdentity.Verify(theirHello.PublicKey, nonce, response.Signature))
            {
                return AuthenticationResult.Failed("Invalid signature from peer");
            }

            // Step 4: Send our Hello for mutual authentication
            var ourHello = new AuthHello
            {
                PeerId = localIdentity.Id.ToBase58(),
                PublicKey = localIdentity.PublicKey
            };

            await SendMessageAsync(connection, AuthMessageType.Hello, ourHello, cts.Token);

            // Step 5: Receive their Challenge
            var (theirChallengeType, theirChallengeData) = await ReceiveMessageAsync(connection, cts.Token);

            if (theirChallengeType != AuthMessageType.Challenge)
            {
                return AuthenticationResult.Failed($"Expected Challenge, got {theirChallengeType}");
            }

            var theirChallenge = MessagePackSerializer.Deserialize<AuthChallenge>(theirChallengeData);

            // Step 6: Send our Response
            var ourSignature = localIdentity.Sign(theirChallenge.Nonce);
            var ourResponse = new AuthResponse { Signature = ourSignature };

            await SendMessageAsync(connection, AuthMessageType.Response, ourResponse, cts.Token);

            // Wait for Success
            var (successType, _) = await ReceiveMessageAsync(connection, cts.Token);

            if (successType != AuthMessageType.Success)
            {
                return AuthenticationResult.Failed($"Expected Success, got {successType}");
            }

            // Send Success
            await SendMessageAsync(connection, AuthMessageType.Success, new AuthSuccess(), cts.Token);

            _logger?.LogInformation("Successfully authenticated peer {PeerId}", connection.RemotePeerId);

            return AuthenticationResult.Succeeded(
                PeerId.Parse(theirHello.PeerId),
                theirHello.PublicKey);
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failed("Authentication timed out");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Authentication handling failed");
            return AuthenticationResult.Failed($"Authentication error: {ex.Message}");
        }
    }

    public bool VerifyPeerIdentity(PeerId peerId, byte[] publicKey)
    {
        var expectedPeerId = PeerId.FromPublicKey(publicKey);
        return peerId == expectedPeerId;
    }

    private static async Task SendMessageAsync<T>(
        PeerConnection connection,
        AuthMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var envelope = new AuthEnvelope
        {
            Type = type,
            Payload = MessagePackSerializer.Serialize(message)
        };

        var data = MessagePackSerializer.Serialize(envelope);
        await connection.SendAsync(data, cancellationToken);
    }

    private static async Task<(AuthMessageType Type, byte[] Data)> ReceiveMessageAsync(
        PeerConnection connection,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<(AuthMessageType, byte[])>();

        Task OnMessageReceived(ReadOnlyMemory<byte> data)
        {
            try
            {
                var envelope = MessagePackSerializer.Deserialize<AuthEnvelope>(data);
                tcs.TrySetResult((envelope.Type, envelope.Payload));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return Task.CompletedTask;
        }

        connection.MessageReceived += OnMessageReceived;

        try
        {
            using var registration = cancellationToken.Register(() =>
                tcs.TrySetCanceled(cancellationToken));

            return await tcs.Task;
        }
        finally
        {
            connection.MessageReceived -= OnMessageReceived;
        }
    }
}

// Authentication message types
internal enum AuthMessageType : byte
{
    Hello = 1,
    Challenge = 2,
    Response = 3,
    Success = 4,
    Failure = 5
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AuthEnvelope
{
    [Key(0)]
    public AuthMessageType Type { get; set; }

    [Key(1)]
    public byte[] Payload { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AuthHello
{
    [Key(0)]
    public string PeerId { get; set; } = "";

    [Key(1)]
    public byte[] PublicKey { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AuthChallenge
{
    [Key(0)]
    public byte[] Nonce { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AuthResponse
{
    [Key(0)]
    public byte[] Signature { get; set; } = [];
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class AuthSuccess { }
