# LowlandTech.Foundry.P2P

The unified P2P networking library - combines peer identity, discovery, security, CRDTs, and WebRTC transport into a single project.

## What This Project Does

P2P provides everything needed for decentralized peer-to-peer communication:

**Identity & Security**
- Ed25519 keypair-based peer identity (`PeerId`, `PeerIdentity`)
- Challenge-response mutual authentication
- X25519 key exchange + encryption
- Trust level management (Unknown → Verified → Trusted → Blocked)

**Discovery**
- LAN discovery via mDNS
- WAN discovery via SignalR signaling
- Composite discovery that combines both

**Transport**
- WebRTC data channels via SIPSorcery
- ICE/STUN/TURN configuration for NAT traversal
- SignalR-based WebRTC signaling

**CRDTs (Conflict-free Replicated Data Types)**
- `GCounter` - Grow-only counter
- `LwwRegister` - Last-Writer-Wins register
- `OrSet` - Observed-Remove set
- `Rga` - Replicated Growable Array (for text)
- `VectorClock` - Causality tracking
- `DeltaSyncProtocol` - Efficient sync
- `LiteDbCrdtStore` - Local persistence

**Plugin Sharing**
- P2P plugin catalog
- Chunked file transfers
- Plugin validation

## Why It's Combined

This project merges what were previously three separate projects (P2P.Core, P2P.Crdt, P2P.WebRTC) because:

1. **Always Used Together** - In practice, you need all three for P2P features to work
2. **Simpler Dependencies** - One project reference instead of three
3. **Cleaner Namespaces** - Everything under `LowlandTech.Foundry.P2P.*`
4. **Easier Maintenance** - Cross-cutting changes don't span multiple projects

## Directory Structure

```
LowlandTech.Foundry.P2P/
├── Identity/       # PeerId, PeerIdentity, PeerInfo
├── Node/           # IP2PNode, P2PNode
├── Transport/      # IDataChannel, PeerConnection
├── Discovery/      # mDNS, SignalR discovery
├── Security/       # Authentication, encryption, trust
├── Plugins/        # P2P plugin sharing
├── Crdt/
│   ├── Types/      # ICrdt, GCounter, LwwRegister, OrSet, Rga, VectorClock
│   ├── Sync/       # Delta sync protocol
│   └── Storage/    # LiteDB persistence
└── WebRTC/
    └── Signaling/  # SignalR signaling channel
```

## Template Customization

**If you need P2P collaboration:**
- Keep this project
- Configure STUN/TURN servers in appsettings.json
- Deploy the P2P.Signaling server for WAN connectivity

**If you don't need P2P:**
- Delete this project
- Delete `src/frontend/LowlandTech.Foundry.Collaboration`
- Delete `src/backend/LowlandTech.Foundry.P2P.Signaling`
- Remove P2P references from Host

## Dependencies

- `NSec.Cryptography` - Ed25519/X25519 cryptography
- `Makaretu.Dns.Multicast` - mDNS for LAN discovery
- `MessagePack` - Binary serialization
- `Microsoft.AspNetCore.SignalR.Client` - WAN signaling
- `SIPSorcery` - WebRTC implementation
- `LiteDB` - Local CRDT storage
