namespace LowlandTech.Foundry.P2P.WebRTC;

/// <summary>
/// ICE (Interactive Connectivity Establishment) configuration for NAT traversal.
/// </summary>
public sealed class IceConfiguration
{
    /// <summary>
    /// STUN servers for NAT traversal.
    /// </summary>
    public List<StunServer> StunServers { get; init; } =
    [
        new("stun:stun.l.google.com:19302"),
        new("stun:stun1.l.google.com:19302"),
        new("stun:stun2.l.google.com:19302")
    ];

    /// <summary>
    /// TURN servers for relay when direct connection isn't possible.
    /// </summary>
    public List<TurnServer> TurnServers { get; init; } = [];

    /// <summary>
    /// ICE transport policy.
    /// </summary>
    public IceTransportPolicy TransportPolicy { get; init; } = IceTransportPolicy.All;

    /// <summary>
    /// ICE gathering timeout in milliseconds.
    /// </summary>
    public int GatheringTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; init; } = 30000;
}

/// <summary>
/// STUN server configuration.
/// </summary>
public sealed record StunServer(string Uri);

/// <summary>
/// TURN server configuration.
/// </summary>
public sealed record TurnServer(
    string Uri,
    string Username,
    string Credential,
    TurnCredentialType CredentialType = TurnCredentialType.Password);

/// <summary>
/// TURN credential type.
/// </summary>
public enum TurnCredentialType
{
    Password,
    OAuthToken
}

/// <summary>
/// ICE transport policy.
/// </summary>
public enum IceTransportPolicy
{
    /// <summary>Use all available transports (STUN + TURN).</summary>
    All,

    /// <summary>Only use TURN relay servers.</summary>
    Relay
}
