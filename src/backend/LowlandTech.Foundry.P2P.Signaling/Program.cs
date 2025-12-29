using LowlandTech.Foundry.P2P.Signaling.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for cross-origin connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// Health check endpoint
app.MapGet("/", () => "P2P Signaling Server");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Map SignalR hub
app.MapHub<SignalingHub>("/signaling");

app.Run();
