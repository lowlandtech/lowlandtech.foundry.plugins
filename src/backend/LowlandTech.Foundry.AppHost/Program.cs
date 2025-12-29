var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL container with database for Identity
// WithDataVolume persists data across container restarts
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("foundry-postgres-data")
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identitydb");

// Add the API project with reference to PostgreSQL
var api = builder.AddProject<Projects.LowlandTech_Foundry_Api>("api")
    .WithReference(identityDb)
    .WaitFor(identityDb);

// Add the Host (Blazor) project with reference to API
builder.AddProject<Projects.LowlandTech_Foundry_Host>("host")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
