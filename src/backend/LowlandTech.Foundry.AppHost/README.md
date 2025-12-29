# LowlandTech.Foundry.AppHost

The .NET Aspire orchestration project - defines how all the services start, connect, and communicate during development.

## What This Project Does

AppHost is the Aspire orchestration entry point:

- Defines the distributed application topology
- Starts PostgreSQL container for the API
- Launches the Host and API projects with proper configuration
- Wires up service discovery so projects can find each other
- Provides the Aspire dashboard for monitoring

## Why It's Standalone

**This project MUST remain separate.** It's not optional.

1. **Aspire Requirement**: This is how .NET Aspire works. The AppHost is a special project type (`Aspire.AppHost` SDK) that orchestrates other projects. It can't be merged into anything.

2. **Development-Time Only**: This project only runs during development. In production, you deploy the Host and API separately (to Kubernetes, Azure, AWS, etc.). The AppHost doesn't ship.

3. **Infrastructure Definition**: Think of it as your local docker-compose equivalent. It spins up dependencies (PostgreSQL) and configures connection strings automatically.

4. **No Runtime Code**: This project contains no application logic. Just orchestration configuration.

## Template Customization

When using this repo as a template:

- Add additional resources (Redis, RabbitMQ, etc.) as needed
- Configure project references for new services
- Adjust resource allocation for containers
- Add the Signaling server if you need P2P WAN discovery

## Example AppHost Configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("foundrydb");

var api = builder.AddProject<Projects.LowlandTech_Foundry_Api>("api")
    .WithReference(postgres);

builder.AddProject<Projects.LowlandTech_Foundry_Host>("host")
    .WithReference(api);

builder.Build().Run();
```

## Dependencies

- `Aspire.Hosting.AppHost` SDK
- `Aspire.Hosting.PostgreSQL` - PostgreSQL container support
- Project references to services being orchestrated

## Should You Merge It?

**You can't.** The Aspire SDK requires AppHost to be its own project. This is a framework constraint, not a design choice.

If you don't want Aspire orchestration, you can:
- Delete this project entirely
- Run the Host and API manually
- Use docker-compose instead
- Configure connection strings manually in appsettings.json

But if you keep Aspire, keep this project as-is.
