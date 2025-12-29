# LowlandTech.Foundry.P2P.Signaling

The WebRTC signaling server - a lightweight service that helps peers find each other and negotiate connections over the internet.

## What This Project Does

P2P.Signaling is a deployable ASP.NET Core service that:

- **Peer Registration**: Allows peers to announce their presence
- **Offer/Answer Relay**: Forwards WebRTC session descriptions between peers
- **ICE Candidate Exchange**: Relays connection candidates for NAT traversal
- **Discovery Hub**: Enables peers to find each other when mDNS (LAN) isn't available

## Why It's Standalone

**This project MUST remain separate.** It's fundamentally different from the others:

1. **Deployable Service**: This runs as its own process/container, not as a library. It's the server that Host instances connect to for WAN discovery.

2. **Different Deployment Target**: While Host runs on user machines, Signaling typically runs in the cloud (Azure, AWS, a VPS). They have completely different hosting requirements.

3. **Minimal Dependencies**: Notice it only uses the base Web SDK. No EF Core, no MudBlazor, no crypto libraries. It's intentionally lightweight.

4. **Centralized Coordination**: Even though the system is "P2P", you need some rendezvous point for peers to find each other over the internet. That's this server.

5. **Security Boundary**: The signaling server only relays connection metadata. It never sees the actual P2P traffic (which is encrypted end-to-end).

## Template Customization

When using this repo as a template:

**If you need WAN P2P:**
- Deploy this service somewhere accessible on the internet
- Configure `P2P:SignalingServer` in Host's appsettings.json
- Consider authentication to prevent abuse

**If you only need LAN P2P:**
- Delete this project entirely
- mDNS discovery works without any server
- Set `EnableWanDiscovery = false` in P2P settings

**If you don't need P2P at all:**
- Delete this along with all P2P.* projects

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Service entry point and SignalR setup |
| `Hubs/SignalingHub.cs` | WebRTC signaling logic |

## Dependencies

- `Microsoft.NET.Sdk.Web` - Minimal web framework
- No other dependencies (intentionally minimal)

## Should You Merge It?

**Absolutely not.**

This is a separate service that runs independently. You can't merge it into a library - it needs its own `Program.cs`, its own hosting, its own deployment pipeline.

The only reasonable change would be to add it to the AppHost for local development orchestration, which would let Aspire spin it up automatically during `dotnet run`.

**If you're simplifying the template:**
- This project is optional - only needed for internet-wide P2P
- Deleting it is fine if you're only doing LAN collaboration
- Don't try to merge it into anything
