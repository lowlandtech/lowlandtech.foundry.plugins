using System.Security.Cryptography;
using MessagePack;

namespace LowlandTech.Foundry.P2P.Core.Identity;

/// <summary>
/// Represents a unique peer identity derived from a cryptographic keypair.
/// The PeerId is the SHA-256 hash of the public key.
/// </summary>
[MessagePackObject]
public readonly struct PeerId : IEquatable<PeerId>
{
    private const int HashSize = 32; // SHA-256 = 32 bytes
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    [Key(0)]
    public byte[] Bytes { get; }

    [SerializationConstructor]
    public PeerId(byte[] bytes)
    {
        if (bytes == null || bytes.Length != HashSize)
            throw new ArgumentException($"PeerId must be {HashSize} bytes", nameof(bytes));
        Bytes = bytes;
    }

    /// <summary>
    /// Creates a PeerId from a public key by computing its SHA-256 hash.
    /// </summary>
    public static PeerId FromPublicKey(ReadOnlySpan<byte> publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return new PeerId(hash);
    }

    /// <summary>
    /// Encodes the PeerId as a Base58 string for human-readable representation.
    /// </summary>
    public string ToBase58()
    {
        return EncodeBase58(Bytes);
    }

    /// <summary>
    /// Parses a Base58-encoded PeerId.
    /// </summary>
    public static PeerId Parse(string base58)
    {
        var bytes = DecodeBase58(base58);
        return new PeerId(bytes);
    }

    /// <summary>
    /// Tries to parse a Base58-encoded PeerId.
    /// </summary>
    public static bool TryParse(string base58, out PeerId peerId)
    {
        try
        {
            peerId = Parse(base58);
            return true;
        }
        catch
        {
            peerId = default;
            return false;
        }
    }

    public bool Equals(PeerId other)
    {
        if (Bytes == null || other.Bytes == null)
            return Bytes == other.Bytes;
        return Bytes.AsSpan().SequenceEqual(other.Bytes);
    }

    public override bool Equals(object? obj) => obj is PeerId other && Equals(other);

    public override int GetHashCode()
    {
        if (Bytes == null || Bytes.Length < 4)
            return 0;
        return BitConverter.ToInt32(Bytes, 0);
    }

    public override string ToString() => ToBase58();

    public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
    public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);

    private static string EncodeBase58(byte[] data)
    {
        // Convert byte array to big integer
        var intData = new System.Numerics.BigInteger(data, isUnsigned: true, isBigEndian: true);
        var result = new System.Text.StringBuilder();

        while (intData > 0)
        {
            var remainder = (int)(intData % 58);
            intData /= 58;
            result.Insert(0, Base58Alphabet[remainder]);
        }

        // Handle leading zeros
        foreach (var b in data)
        {
            if (b == 0)
                result.Insert(0, Base58Alphabet[0]);
            else
                break;
        }

        return result.ToString();
    }

    private static byte[] DecodeBase58(string base58)
    {
        var intData = System.Numerics.BigInteger.Zero;

        foreach (var c in base58)
        {
            var index = Base58Alphabet.IndexOf(c);
            if (index < 0)
                throw new FormatException($"Invalid Base58 character: {c}");

            intData = intData * 58 + index;
        }

        var bytes = intData.ToByteArray(isUnsigned: true, isBigEndian: true);

        // Handle leading zeros
        var leadingZeros = base58.TakeWhile(c => c == Base58Alphabet[0]).Count();
        if (leadingZeros > 0)
        {
            var result = new byte[leadingZeros + bytes.Length];
            bytes.CopyTo(result, leadingZeros);
            return result;
        }

        return bytes;
    }
}
