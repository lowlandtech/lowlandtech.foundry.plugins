# LowlandTech.Foundry.Collaboration

The unified collaboration library - combines collaboration features and UI components into a single Razor Class Library.

## What This Project Does

Collaboration provides high-level features built on P2P infrastructure:

**Chat**
- `ChatRoom` - CRDT-backed chat room with history
- `ChatMessage` - Message model with sender, timestamp, reactions

**Documents**
- `CollaborativeDocument` - Real-time document editing using RGA
- `DocumentVersion` - Version tracking

**Presence**
- `IPresenceService` / `CrdtPresenceService` - Who's online
- `CursorInfo` - Cursor positions and selections

**UI Components (Razor)**
- `PeerStatusPanel` - Shows online peers with status
- `ChatWindow` - Real-time chat interface
- `CollaborationWorkspace` - Document editor with sidebar
- `PeerDiscoveryDialog` - Find and connect to peers

**Models**
- `DiscoveredPeer` - Peer info for discovery UI
- `PeerInvite` - Connection invitation model

## Why It's Combined

This project merges what were previously two separate projects (Collaboration, Collaboration.UI) because:

1. **Always Used Together** - The UI needs the logic; the logic needs rendering
2. **Single RCL** - One Razor Class Library that provides everything
3. **Simpler Dependencies** - One reference from Host
4. **Feature Cohesion** - Chat, documents, and presence are a cohesive feature set

## Directory Structure

```
LowlandTech.Foundry.Collaboration/
├── Chat/           # ChatRoom, ChatMessage
├── Documents/      # CollaborativeDocument, DocumentVersion
├── Presence/       # PresenceService, CursorInfo
├── Components/     # Razor components (UI)
├── Models/         # DiscoveredPeer, PeerInvite
└── _Imports.razor  # Shared using statements
```

## Template Customization

**If you want collaboration features:**
- Keep this project
- Reference it from Host
- Drop components into your pages: `<ChatWindow />`, `<PeerStatusPanel />`

**Extending collaboration:**
- Add new CRDT-based features (whiteboard, kanban, etc.)
- Create new components in `Components/`
- Add models in `Models/`

**If you don't need collaboration:**
- Delete this project
- Delete `src/frontend/LowlandTech.Foundry.P2P`
- Delete `src/backend/LowlandTech.Foundry.P2P.Signaling`
- Remove collaboration references from Host

## Dependencies

- `MudBlazor` - UI framework
- `MessagePack` - Serialization
- `LowlandTech.Foundry.P2P` - P2P infrastructure
