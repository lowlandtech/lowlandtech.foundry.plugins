using System.Security.Cryptography;
using LowlandTech.Foundry.P2P.Core.Identity;
using Microsoft.Extensions.Logging;

namespace LowlandTech.Foundry.P2P.Core.Plugins;

/// <summary>
/// Validates plugins for security before installation.
/// </summary>
public sealed class PluginValidator
{
    private readonly ILogger<PluginValidator>? _logger;
    private readonly HashSet<string> _blockedHashes = new();
    private readonly HashSet<string> _trustedPublicKeys = new();

    public PluginValidator(ILogger<PluginValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a plugin's integrity and signature.
    /// </summary>
    public PluginValidationResult Validate(PluginMetadata metadata, byte[] pluginData)
    {
        var result = new PluginValidationResult { PluginId = metadata.Id };

        // Check if hash is blocked
        if (_blockedHashes.Contains(metadata.ContentHash))
        {
            result.IsValid = false;
            result.Errors.Add("Plugin hash is blocked");
            result.TrustLevel = PluginTrustLevel.Blocked;
            _logger?.LogWarning("Plugin {PluginId} is blocked", metadata.Id);
            return result;
        }

        // Verify content hash
        var computedHash = ComputeHash(pluginData);
        if (computedHash != metadata.ContentHash)
        {
            result.IsValid = false;
            result.Errors.Add("Content hash mismatch - plugin may be corrupted or tampered with");
            _logger?.LogWarning("Plugin {PluginId} hash mismatch: expected {Expected}, got {Actual}",
                metadata.Id, metadata.ContentHash, computedHash);
            return result;
        }

        result.HashVerified = true;

        // Verify size
        if (pluginData.Length != metadata.Size)
        {
            result.IsValid = false;
            result.Errors.Add($"Size mismatch: expected {metadata.Size}, got {pluginData.Length}");
            return result;
        }

        result.SizeVerified = true;

        // Verify signature if present
        if (metadata.Signature.Length > 0 && metadata.AuthorPublicKey.Length > 0)
        {
            try
            {
                var signatureValid = VerifySignature(pluginData, metadata.Signature, metadata.AuthorPublicKey);
                if (signatureValid)
                {
                    result.SignatureVerified = true;

                    // Check if author is trusted
                    var publicKeyBase64 = Convert.ToBase64String(metadata.AuthorPublicKey);
                    if (_trustedPublicKeys.Contains(publicKeyBase64))
                    {
                        result.TrustLevel = PluginTrustLevel.Trusted;
                    }
                    else
                    {
                        result.TrustLevel = PluginTrustLevel.Verified;
                    }
                }
                else
                {
                    result.Warnings.Add("Signature verification failed - plugin may not be from stated author");
                    result.TrustLevel = PluginTrustLevel.Untrusted;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Signature verification error: {ex.Message}");
                _logger?.LogError(ex, "Error verifying signature for plugin {PluginId}", metadata.Id);
            }
        }
        else
        {
            result.Warnings.Add("Plugin is not signed - cannot verify author");
            result.TrustLevel = PluginTrustLevel.Unknown;
        }

        // Basic security checks
        var securityChecks = PerformSecurityChecks(pluginData, metadata);
        result.Warnings.AddRange(securityChecks.Warnings);
        result.Errors.AddRange(securityChecks.Errors);

        if (securityChecks.Errors.Count > 0)
        {
            result.IsValid = false;
        }

        // Set final validity
        if (result.Errors.Count == 0)
        {
            result.IsValid = true;
        }

        _logger?.LogInformation("Plugin {PluginId} validation: Valid={IsValid}, Trust={TrustLevel}",
            metadata.Id, result.IsValid, result.TrustLevel);

        return result;
    }

    /// <summary>
    /// Computes SHA-256 hash of data.
    /// </summary>
    public static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA-256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Adds a hash to the blocklist.
    /// </summary>
    public void BlockHash(string hash)
    {
        _blockedHashes.Add(hash.ToLowerInvariant());
    }

    /// <summary>
    /// Removes a hash from the blocklist.
    /// </summary>
    public void UnblockHash(string hash)
    {
        _blockedHashes.Remove(hash.ToLowerInvariant());
    }

    /// <summary>
    /// Adds a public key to the trusted list.
    /// </summary>
    public void TrustPublicKey(byte[] publicKey)
    {
        _trustedPublicKeys.Add(Convert.ToBase64String(publicKey));
    }

    /// <summary>
    /// Removes a public key from the trusted list.
    /// </summary>
    public void UntrustPublicKey(byte[] publicKey)
    {
        _trustedPublicKeys.Remove(Convert.ToBase64String(publicKey));
    }

    private static bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey)
    {
        try
        {
            return PeerIdentity.Verify(publicKey, data, signature);
        }
        catch
        {
            return false;
        }
    }

    private SecurityCheckResult PerformSecurityChecks(byte[] pluginData, PluginMetadata metadata)
    {
        var result = new SecurityCheckResult();

        // Check for suspiciously small or large plugins
        if (pluginData.Length < 1024)
        {
            result.Warnings.Add("Plugin is suspiciously small");
        }

        if (pluginData.Length > 100 * 1024 * 1024) // 100 MB
        {
            result.Warnings.Add("Plugin is very large (>100MB)");
        }

        // Check version format
        if (!IsValidVersionFormat(metadata.Version))
        {
            result.Warnings.Add($"Invalid version format: {metadata.Version}");
        }

        // Check for empty required fields
        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            result.Errors.Add("Plugin name is required");
        }

        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            result.Errors.Add("Plugin ID is required");
        }

        return result;
    }

    private static bool IsValidVersionFormat(string version)
    {
        // Basic semantic version check
        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 4)
            return false;

        foreach (var part in parts)
        {
            // Allow prerelease suffix on last part
            var numPart = part.Split('-')[0];
            if (!int.TryParse(numPart, out _))
                return false;
        }

        return true;
    }

    private sealed class SecurityCheckResult
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
    }
}

/// <summary>
/// Result of plugin validation.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Plugin ID that was validated.
    /// </summary>
    public string PluginId { get; set; } = "";

    /// <summary>
    /// Whether the plugin passed validation.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Whether the content hash was verified.
    /// </summary>
    public bool HashVerified { get; set; }

    /// <summary>
    /// Whether the size was verified.
    /// </summary>
    public bool SizeVerified { get; set; }

    /// <summary>
    /// Whether the signature was verified.
    /// </summary>
    public bool SignatureVerified { get; set; }

    /// <summary>
    /// Trust level determined by validation.
    /// </summary>
    public PluginTrustLevel TrustLevel { get; set; } = PluginTrustLevel.Unknown;

    /// <summary>
    /// Validation errors (fatal issues).
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Validation warnings (non-fatal issues).
    /// </summary>
    public List<string> Warnings { get; } = new();
}
