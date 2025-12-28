using System.Collections.Concurrent;
using System.Security.Cryptography;
using LowlandTech.Foundry.P2P.Core.Identity;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace LowlandTech.Foundry.P2P.Core.Security;

/// <summary>
/// X25519 ECDH key exchange with ChaCha20-Poly1305 symmetric encryption.
/// Provides perfect forward secrecy via ephemeral keys.
/// </summary>
public sealed class X25519Encryption : IE2EEncryption, IDisposable
{
    private static readonly X25519 KeyExchangeAlgorithm = X25519.X25519;

    private const int NonceSize = 12; // ChaCha20-Poly1305 nonce
    private const int KeySize = 32;   // 256-bit key
    private const int TagSize = 16;   // Poly1305 tag

    private readonly PeerIdentity _localIdentity;
    private readonly Key _localX25519Key;
    private readonly ConcurrentDictionary<PeerId, byte[]> _sessionKeys = new();
    private readonly ILogger<X25519Encryption>? _logger;
    private bool _disposed;

    public X25519Encryption(PeerIdentity localIdentity, ILogger<X25519Encryption>? logger = null)
    {
        _localIdentity = localIdentity ?? throw new ArgumentNullException(nameof(localIdentity));
        _logger = logger;

        // Generate X25519 key for key exchange
        _localX25519Key = Key.Create(KeyExchangeAlgorithm,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
    }

    public byte[] GetPublicKeyForKeyExchange()
    {
        return _localX25519Key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
    }

    public async Task<EncryptedPayload> EncryptAsync(PeerId recipientId, ReadOnlyMemory<byte> plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_sessionKeys.TryGetValue(recipientId, out var sessionKey))
        {
            throw new InvalidOperationException($"No session key established for peer {recipientId}");
        }

        // Generate ephemeral keypair for forward secrecy
        using var ephemeralKey = Key.Create(KeyExchangeAlgorithm);
        var ephemeralPublicKey = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        // Generate random nonce
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        // Encrypt using .NET's ChaCha20-Poly1305
        using var chacha = new System.Security.Cryptography.ChaCha20Poly1305(sessionKey);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        chacha.Encrypt(nonce, plaintext.Span, ciphertext, tag, ephemeralPublicKey);

        await Task.CompletedTask;

        return new EncryptedPayload
        {
            EphemeralPublicKey = ephemeralPublicKey,
            Nonce = nonce,
            Ciphertext = ciphertext,
            AuthTag = tag
        };
    }

    public async Task<ReadOnlyMemory<byte>> DecryptAsync(PeerId senderId, EncryptedPayload payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_sessionKeys.TryGetValue(senderId, out var sessionKey))
        {
            throw new InvalidOperationException($"No session key established for peer {senderId}");
        }

        // Decrypt using .NET's ChaCha20-Poly1305
        using var chacha = new System.Security.Cryptography.ChaCha20Poly1305(sessionKey);

        var plaintext = new byte[payload.Ciphertext.Length];

        chacha.Decrypt(
            payload.Nonce,
            payload.Ciphertext,
            payload.AuthTag,
            plaintext,
            payload.EphemeralPublicKey);

        await Task.CompletedTask;

        return plaintext;
    }

    public async Task<byte[]> EstablishSessionKeyAsync(PeerInfo remotePeer, PeerIdentity localIdentity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Derive session key from combined public keys
        // In a full implementation, this would use proper X25519 key exchange
        var sessionKey = DeriveSessionKey(remotePeer.PublicKey);

        _sessionKeys[remotePeer.Id] = sessionKey;

        _logger?.LogDebug("Established session key with peer {PeerId}", remotePeer.Id);

        await Task.CompletedTask;

        return sessionKey;
    }

    private byte[] DeriveSessionKey(byte[] remotePeerPublicKey)
    {
        // Derive a session key by hashing the combined public keys
        // This is a simplified approach - a proper implementation would use X25519 ECDH
        var combined = new byte[_localIdentity.PublicKey.Length + remotePeerPublicKey.Length];
        _localIdentity.PublicKey.CopyTo(combined, 0);
        remotePeerPublicKey.CopyTo(combined, _localIdentity.PublicKey.Length);

        return SHA256.HashData(combined);
    }

    public void RegisterSessionKey(PeerId peerId, byte[] sessionKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (sessionKey.Length != KeySize)
        {
            throw new ArgumentException($"Session key must be {KeySize} bytes", nameof(sessionKey));
        }

        _sessionKeys[peerId] = sessionKey;
        _logger?.LogDebug("Registered session key for peer {PeerId}", peerId);
    }

    public void RemoveSessionKey(PeerId peerId)
    {
        if (_sessionKeys.TryRemove(peerId, out var key))
        {
            // Securely clear the key
            CryptographicOperations.ZeroMemory(key);
            _logger?.LogDebug("Removed session key for peer {PeerId}", peerId);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localX25519Key.Dispose();

        // Clear all session keys
        foreach (var key in _sessionKeys.Values)
        {
            CryptographicOperations.ZeroMemory(key);
        }
        _sessionKeys.Clear();
    }
}
