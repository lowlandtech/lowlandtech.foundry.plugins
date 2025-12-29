# LowlandTech.Foundry.Api

The backend REST API - handles authentication, user management, data persistence, and P2P settings storage.

## What This Project Does

Api provides:

- **ASP.NET Core Identity**: User registration, login, password management
- **Entity Framework Core**: Database access with PostgreSQL
- **P2P Settings Persistence**: Stores user P2P preferences (trusted peers, discovery settings)
- **REST Endpoints**: JSON APIs consumed by the Host

## Why It's Standalone

**This project SHOULD remain separate from the Host.** Here's why:

1. **Different Runtime Model**: The API is stateless REST. The Host is stateful Blazor Server with SignalR circuits. Mixing them creates confusion about state management.

2. **Database Access Isolation**: Only the API touches the database. The Host communicates via HTTP. This prevents accidental tight coupling to your data model.

3. **Security Boundary**: Authentication happens at the API layer. Tokens are issued here and validated here. Keeping this separate makes security audits clearer.

4. **Deployment Flexibility**: You might deploy the API to Azure App Service while running the Host in a container. Or use different scaling strategies. Or swap the API for a different backend entirely.

5. **Aspire Pattern**: This follows the .NET Aspire model where APIs and frontends are separate services that can be orchestrated together.

## Template Customization

When using this repo as a template:

- Add your domain entities to `Data/Entities/`
- Create EF migrations for schema changes
- Add new endpoints in `Program.cs` or create Controllers
- Configure your database provider (the template uses PostgreSQL via Aspire)
- Extend Identity with custom user properties if needed

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Service configuration and endpoint mapping |
| `Data/ApplicationDbContext.cs` | EF Core context with Identity tables |
| `Data/Entities/UserP2PSettings.cs` | P2P preferences per user |

## Dependencies

- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` - Database access
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` - User management
- `ServiceDefaults` - OpenTelemetry, health checks, resilience

## Should You Merge It?

**No.** The API genuinely needs to be separate from the Host. The Blazor Server model doesn't mesh well with REST API patterns in the same project. You'd end up with confused routing, mixed authentication schemes, and deployment headaches.

However, if you're building a simpler app without P2P collaboration, you could:
- Remove P2P-related endpoints
- Strip down to just authentication
- Or eliminate the API entirely and use Blazor Server's native authentication with a local database
