using NSec.Cryptography;

namespace LowlandTech.Foundry.P2P.Identity;

/// <summary>
/// Represents a peer's cryptographic identity using Ed25519 keys.
/// </summary>
public sealed class PeerIdentity : IDisposable
{
    private static readonly Ed25519 Algorithm = Ed25519.Ed25519;
    private readonly Key? _privateKey;
    private bool _disposed;

    /// <summary>
    /// The unique peer identifier (SHA-256 hash of the public key).
    /// </summary>
    public PeerId Id { get; }

    /// <summary>
    /// The Ed25519 public key.
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Whether this identity has the private key (can sign messages).
    /// </summary>
    public bool HasPrivateKey => _privateKey != null;

    /// <summary>
    /// Display name for this peer.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Optional avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// When this identity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    private PeerIdentity(Key privateKey, string displayName)
    {
        _privateKey = privateKey;
        PublicKey = privateKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        Id = PeerId.FromPublicKey(PublicKey);
        DisplayName = displayName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private PeerIdentity(byte[] publicKey, string displayName)
    {
        _privateKey = null;
        PublicKey = publicKey;
        Id = PeerId.FromPublicKey(publicKey);
        DisplayName = displayName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Generates a new peer identity with a fresh Ed25519 keypair.
    /// </summary>
    public static PeerIdentity Generate(string displayName)
    {
        var privateKey = Key.Create(Algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        return new PeerIdentity(privateKey, displayName);
    }

    /// <summary>
    /// Creates a peer identity from an existing keypair.
    /// </summary>
    public static PeerIdentity FromPrivateKey(byte[] privateKeyBytes, string displayName)
    {
        var privateKey = Key.Import(Algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        return new PeerIdentity(privateKey, displayName);
    }

    /// <summary>
    /// Creates a public-only peer identity (cannot sign, used for remote peers).
    /// </summary>
    public static PeerIdentity FromPublicKey(byte[] publicKey, string displayName)
    {
        return new PeerIdentity(publicKey, displayName);
    }

    /// <summary>
    /// Exports the private key (throws if this identity doesn't have one).
    /// </summary>
    public byte[] ExportPrivateKey()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_privateKey == null)
            throw new InvalidOperationException("This identity does not have a private key");

        return _privateKey.Export(KeyBlobFormat.RawPrivateKey);
    }

    /// <summary>
    /// Signs data using the private key.
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_privateKey == null)
            throw new InvalidOperationException("This identity does not have a private key");

        return Algorithm.Sign(_privateKey, data);
    }

    /// <summary>
    /// Verifies a signature using this identity's public key.
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var publicKey = NSec.Cryptography.PublicKey.Import(Algorithm, PublicKey, KeyBlobFormat.RawPublicKey);
        return Algorithm.Verify(publicKey, data, signature);
    }

    /// <summary>
    /// Verifies a signature using a public key.
    /// </summary>
    public static bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        var pubKey = NSec.Cryptography.PublicKey.Import(Algorithm, publicKey, KeyBlobFormat.RawPublicKey);
        return Algorithm.Verify(pubKey, data, signature);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _privateKey?.Dispose();
        _disposed = true;
    }
}
