using LowlandTech.Foundry.P2P.Core.Identity;

namespace LowlandTech.Foundry.P2P.Core.Security;

/// <summary>
/// Interface for end-to-end encryption between peers.
/// </summary>
public interface IE2EEncryption
{
    /// <summary>
    /// Encrypts data for a specific peer.
    /// </summary>
    Task<EncryptedPayload> EncryptAsync(PeerId recipientId, ReadOnlyMemory<byte> plaintext);

    /// <summary>
    /// Decrypts data received from a peer.
    /// </summary>
    Task<ReadOnlyMemory<byte>> DecryptAsync(PeerId senderId, EncryptedPayload payload);

    /// <summary>
    /// Establishes a session key with a peer using key exchange.
    /// </summary>
    Task<byte[]> EstablishSessionKeyAsync(PeerInfo remotePeer, PeerIdentity localIdentity);

    /// <summary>
    /// Registers a session key for a peer.
    /// </summary>
    void RegisterSessionKey(PeerId peerId, byte[] sessionKey);

    /// <summary>
    /// Removes the session key for a peer.
    /// </summary>
    void RemoveSessionKey(PeerId peerId);
}

/// <summary>
/// Encrypted payload with all necessary cryptographic data.
/// </summary>
public sealed record EncryptedPayload
{
    /// <summary>
    /// Ephemeral public key used for this message (for forward secrecy).
    /// </summary>
    public required byte[] EphemeralPublicKey { get; init; }

    /// <summary>
    /// Nonce used for encryption.
    /// </summary>
    public required byte[] Nonce { get; init; }

    /// <summary>
    /// Encrypted ciphertext.
    /// </summary>
    public required byte[] Ciphertext { get; init; }

    /// <summary>
    /// Authentication tag.
    /// </summary>
    public required byte[] AuthTag { get; init; }
}
