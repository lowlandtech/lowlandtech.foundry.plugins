# Source Structure

This folder is organized into three subfolders to provide clear separation between different concerns:

```
src/
├── frontend/    # UI and client-side libraries
├── backend/     # Server-side services
└── examples/    # Example plugins (delete for production)
```

## Why This Split?

### frontend/

Contains everything that runs in the user's browser or is consumed by the UI:

| Project | Purpose |
|---------|---------|
| **PluginCore** | Plugin framework - the shared contract between Host and plugins |
| **Host** | Blazor Server application - the main UI shell |
| **P2P** | Peer-to-peer networking, CRDTs, and WebRTC transport |
| **Collaboration** | Chat, documents, presence features + UI components |

These projects share a common concern: they're about what users see and interact with. Even P2P, while it has networking code, runs client-side and enables real-time collaboration in the UI.

### backend/

Contains server-side services that run independently:

| Project | Purpose |
|---------|---------|
| **Api** | REST API with Identity authentication and database |
| **AppHost** | Aspire orchestration for local development |
| **ServiceDefaults** | Shared Aspire configuration (telemetry, health checks) |
| **P2P.Signaling** | WebRTC signaling server for WAN peer discovery |

These are deployable services. The Api and P2P.Signaling are separate processes that might run on different servers or scale independently.

### examples/

Contains demonstration code that you'd remove in a real application:

| Project | Purpose |
|---------|---------|
| **SamplePlugin** | Shows how to create plugins with menu items |
| **PremiumTheme** | Shows how to create theme plugins |

These exist to teach the plugin system. Delete them when building your own app, or use them as starting points for your actual plugins.

## Benefits of This Organization

1. **Clear deployment boundaries** - Backend services deploy together, frontend assembles into the Host
2. **Easier navigation** - Looking for UI code? It's in frontend. API endpoints? Backend.
3. **Simpler cleanup** - Don't need examples? Delete one folder.
4. **Logical dependencies** - Frontend projects reference each other; backend services are more isolated
5. **Team organization** - If you have separate frontend/backend teams, they know where to work

## When to Add New Projects

- **New UI feature or component library** → `frontend/`
- **New API or background service** → `backend/`
- **New example or demo plugin** → `examples/`
- **Shared code needed by both frontend and backend** → Consider if it belongs in PluginCore, or create a new `shared/` folder
