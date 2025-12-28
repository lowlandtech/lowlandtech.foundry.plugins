using LowlandTech.Foundry.P2P.Core.Identity;
using LowlandTech.Foundry.P2P.Core.Transport;

namespace LowlandTech.Foundry.P2P.Core.Security;

/// <summary>
/// Interface for authenticating peer identity during connection establishment.
/// </summary>
public interface IPeerAuthenticator
{
    /// <summary>
    /// Authenticates a peer connection using challenge-response protocol.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        PeerConnection connection,
        PeerIdentity localIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles incoming authentication request from a remote peer.
    /// </summary>
    Task<AuthenticationResult> HandleAuthenticationAsync(
        PeerConnection connection,
        PeerIdentity localIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that a peer's public key matches their claimed identity.
    /// </summary>
    bool VerifyPeerIdentity(PeerId peerId, byte[] publicKey);
}

/// <summary>
/// Result of peer authentication.
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The authenticated peer's ID (if successful).
    /// </summary>
    public PeerId? PeerId { get; init; }

    /// <summary>
    /// The authenticated peer's public key (if successful).
    /// </summary>
    public byte[]? PublicKey { get; init; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Session key established during authentication (for encryption).
    /// </summary>
    public byte[]? SessionKey { get; init; }

    public static AuthenticationResult Succeeded(PeerId peerId, byte[] publicKey, byte[]? sessionKey = null)
        => new()
        {
            Success = true,
            PeerId = peerId,
            PublicKey = publicKey,
            SessionKey = sessionKey
        };

    public static AuthenticationResult Failed(string errorMessage)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
