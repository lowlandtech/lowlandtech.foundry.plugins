# Frontend

Client-side libraries and the Blazor Server application that users interact with.

## Projects

| Project | Description |
|---------|-------------|
| **PluginCore** | Plugin framework - attributes, catalogs, theming, menu discovery |
| **Host** | Blazor Server shell - layout, navigation, pages, authentication UI |
| **P2P** | Peer-to-peer networking - identity, discovery, CRDTs, WebRTC |
| **Collaboration** | Collaboration features - chat, documents, presence, UI components |

## Dependency Graph

```
PluginCore (no dependencies - this is the shared contract)
    ↑
    ├── Host (references PluginCore, P2P, Collaboration)
    ├── P2P (standalone, no PluginCore dependency)
    └── Collaboration (references P2P)
```

## PluginCore

The foundation that plugins build on. Contains:

- `[MenuItem]` and `[PageLayout]` attributes for page registration
- Plugin catalogs (Assembly, Folder, NuGet loading)
- `ITheme` and `ThemeService` for theming
- `IPluginMenuProvider` for dynamic navigation

**Key principle:** Keep this minimal. Every dependency here becomes a dependency for all plugins.

## Host

The runnable Blazor Server application. Contains:

- `MainLayout.razor` - App shell with sidebar and top bar
- Page components (Home, Settings, P2P Settings, Collaboration)
- Authentication services (talks to the backend API)
- Theme storage provider

**This is where everything comes together.** Host loads plugins, renders their pages, and provides the overall UX.

## P2P

Peer-to-peer networking library. Contains:

- **Identity/** - Ed25519 keypairs, peer IDs
- **Discovery/** - mDNS (LAN) and SignalR (WAN) peer discovery
- **Security/** - Authentication, encryption, trust management
- **Transport/** - Data channel abstractions
- **Crdt/** - Conflict-free replicated data types (GCounter, LwwRegister, OrSet, Rga)
- **WebRTC/** - SIPSorcery-based WebRTC implementation

**Optional feature.** If you don't need P2P collaboration, delete this project and Collaboration.

## Collaboration

High-level collaboration features built on P2P. Contains:

- **Chat/** - ChatRoom, ChatMessage
- **Documents/** - CollaborativeDocument with real-time editing
- **Presence/** - Who's online, cursor positions
- **Components/** - Ready-to-use Razor components (ChatWindow, PeerStatusPanel, etc.)
- **Models/** - DiscoveredPeer, PeerInvite

**Depends on P2P.** If you remove P2P, remove this too.

## Adding a New Frontend Project

1. Create the project in this folder
2. Add it to `Plugins.slnx` under the `/src/frontend/` folder
3. Reference it from Host if needed
4. If it's a plugin, reference PluginCore instead
