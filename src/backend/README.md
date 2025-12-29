# Backend

Server-side services that run independently from the frontend.

## Projects

| Project | Description |
|---------|-------------|
| **Api** | REST API with Identity authentication and PostgreSQL database |
| **AppHost** | .NET Aspire orchestration for local development |
| **ServiceDefaults** | Shared Aspire configuration (telemetry, resilience, health checks) |
| **P2P.Signaling** | WebRTC signaling server for WAN peer discovery |

## Dependency Graph

```
ServiceDefaults (shared infrastructure)
    ↑
    ├── Api (references ServiceDefaults)
    └── P2P.Signaling (standalone, minimal dependencies)

AppHost (references Api and Host for orchestration)
```

## Api

The REST API that the frontend talks to. Contains:

- ASP.NET Core Identity for user authentication
- Entity Framework Core with PostgreSQL
- User registration, login, token refresh endpoints
- P2P settings storage (trusted peers, discovery preferences)

**Database:** Uses Aspire's PostgreSQL integration. Connection strings are injected automatically during development.

### Key Endpoints

- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Get tokens
- `POST /api/auth/refresh` - Refresh tokens
- `GET/PUT /api/p2p-settings` - User P2P preferences
- `POST/DELETE /api/p2p-settings/trust/{peerId}` - Manage trusted peers
- `POST/DELETE /api/p2p-settings/block/{peerId}` - Manage blocked peers

## AppHost

.NET Aspire orchestration project. Contains:

- `Program.cs` that defines the distributed application
- Spins up PostgreSQL container
- Launches Api and Host with proper configuration
- Provides the Aspire dashboard for monitoring

**Development only.** This doesn't ship to production. In production, deploy Api and Host separately.

## ServiceDefaults

Shared Aspire infrastructure configuration. Contains:

- OpenTelemetry setup (tracing, metrics, logging)
- Health check endpoints
- HTTP client resilience (retry policies, circuit breakers)
- Service discovery integration

**Standard Aspire pattern.** Both Api and Host reference this to get consistent observability.

## P2P.Signaling

WebRTC signaling server for peer discovery over the internet. Contains:

- SignalR hub for WebRTC offer/answer exchange
- ICE candidate relay
- Peer registration and discovery

**Only needed for WAN P2P.** If you only need LAN collaboration (mDNS), you can skip deploying this.

### How Signaling Works

1. Peers connect to the signaling server via SignalR
2. When Peer A wants to connect to Peer B, it sends an "offer" through the server
3. Server relays the offer to Peer B
4. Peer B sends an "answer" back through the server
5. ICE candidates are exchanged to find the best connection path
6. Once connected, peers communicate directly (server is no longer needed)

## Adding a New Backend Project

1. Create the project in this folder
2. Add it to `Plugins.slnx` under the `/src/backend/` folder
3. Reference ServiceDefaults for consistent infrastructure
4. Add to AppHost if it needs orchestration during development
