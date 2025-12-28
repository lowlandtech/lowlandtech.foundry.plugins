using System.Security.Claims;
using LowlandTech.Foundry.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add PostgreSQL with Aspire integration
builder.AddNpgsqlDbContext<ApplicationDbContext>("identitydb");

// Add Identity services with cookie authentication
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    // Configure password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Configure sign-in requirements
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "Foundry.Auth";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // Return 401 instead of redirecting for API calls
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorizationBuilder();

// Add CORS for Blazor Host
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorHost", policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000", "https://localhost:7001", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Map service default endpoints (health checks)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Auto-migrate database in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("BlazorHost");

app.UseAuthentication();
app.UseAuthorization();

// Map Identity API endpoints
app.MapIdentityApi<ApplicationUser>();

// User info endpoint for the Blazor host (allows anonymous to check auth state)
app.MapGet("/api/auth/user", (HttpContext context) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated != true)
    {
        return Results.Ok(new UserInfo(false, null, []));
    }

    return Results.Ok(new UserInfo(
        true,
        user.Identity.Name,
        user.Claims.Select(c => new ClaimInfo(c.Type, c.Value)).ToArray()
    ));
}).AllowAnonymous();

// Logout endpoint
app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok();
}).RequireAuthorization();

// Get user profile
app.MapGet("/api/profile", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new UserProfile(
        user.Email,
        user.UserName,
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.PhoneNumber,
        new AddressInfo(
            user.AddressLine1,
            user.AddressLine2,
            user.City,
            user.State,
            user.PostalCode,
            user.Country
        ),
        user.CreatedAt,
        user.UpdatedAt
    ));
}).RequireAuthorization();

// Update user profile
app.MapPut("/api/profile", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    UpdateProfileRequest request) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null)
    {
        return Results.NotFound();
    }

    user.FirstName = request.FirstName;
    user.LastName = request.LastName;
    user.DisplayName = request.DisplayName;
    user.PhoneNumber = request.PhoneNumber;
    user.AddressLine1 = request.Address?.Line1;
    user.AddressLine2 = request.Address?.Line2;
    user.City = request.Address?.City;
    user.State = request.Address?.State;
    user.PostalCode = request.Address?.PostalCode;
    user.Country = request.Address?.Country;
    user.UpdatedAt = DateTime.UtcNow;

    var result = await userManager.UpdateAsync(user);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
    }

    return Results.Ok();
}).RequireAuthorization();

// Change password
app.MapPost("/api/profile/change-password", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ChangePasswordRequest request) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null)
    {
        return Results.NotFound();
    }

    var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
    }

    return Results.Ok();
}).RequireAuthorization();

// Get P2P settings for current user
app.MapGet("/api/p2p-settings", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null)
    {
        return Results.NotFound();
    }

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null)
    {
        // Create default settings for the user
        settings = new UserP2PSettings { UserId = user.Id };
        db.UserP2PSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    return Results.Ok(new UserP2PSettingsResponse(
        settings.PeerId,
        settings.P2PDisplayName,
        settings.Status,
        settings.StatusMessage,
        settings.EnableLanDiscovery,
        settings.EnableWanDiscovery,
        settings.RequirePeerVerification,
        settings.AutoAcceptTrustedPeers,
        settings.EnablePluginSharing,
        settings.AutoDownloadFromTrusted,
        settings.RequireSignedPlugins,
        ParsePeerList(settings.TrustedPeerIds),
        ParsePeerList(settings.BlockedPeerIds),
        settings.CreatedAt,
        settings.UpdatedAt
    ));
}).RequireAuthorization();

// Update P2P settings for current user
app.MapPut("/api/p2p-settings", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    UpdateP2PSettingsRequest request) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null)
    {
        return Results.NotFound();
    }

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null)
    {
        settings = new UserP2PSettings { UserId = user.Id };
        db.UserP2PSettings.Add(settings);
    }

    if (request.P2PDisplayName != null) settings.P2PDisplayName = request.P2PDisplayName;
    if (request.Status != null) settings.Status = request.Status;
    if (request.StatusMessage != null) settings.StatusMessage = request.StatusMessage;
    if (request.EnableLanDiscovery.HasValue) settings.EnableLanDiscovery = request.EnableLanDiscovery.Value;
    if (request.EnableWanDiscovery.HasValue) settings.EnableWanDiscovery = request.EnableWanDiscovery.Value;
    if (request.RequirePeerVerification.HasValue) settings.RequirePeerVerification = request.RequirePeerVerification.Value;
    if (request.AutoAcceptTrustedPeers.HasValue) settings.AutoAcceptTrustedPeers = request.AutoAcceptTrustedPeers.Value;
    if (request.EnablePluginSharing.HasValue) settings.EnablePluginSharing = request.EnablePluginSharing.Value;
    if (request.AutoDownloadFromTrusted.HasValue) settings.AutoDownloadFromTrusted = request.AutoDownloadFromTrusted.Value;
    if (request.RequireSignedPlugins.HasValue) settings.RequireSignedPlugins = request.RequireSignedPlugins.Value;
    settings.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// Trust a peer
app.MapPost("/api/p2p-settings/trust", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    TrustPeerRequest request) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null) return Results.NotFound();

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null)
    {
        settings = new UserP2PSettings { UserId = user.Id };
        db.UserP2PSettings.Add(settings);
    }

    var trustedList = ParsePeerList(settings.TrustedPeerIds);
    var blockedList = ParsePeerList(settings.BlockedPeerIds);

    // Remove from blocked if present
    blockedList.Remove(request.PeerId);

    // Add to trusted if not already present
    if (!trustedList.Contains(request.PeerId))
    {
        trustedList.Add(request.PeerId);
    }

    settings.TrustedPeerIds = System.Text.Json.JsonSerializer.Serialize(trustedList);
    settings.BlockedPeerIds = System.Text.Json.JsonSerializer.Serialize(blockedList);
    settings.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// Block a peer
app.MapPost("/api/p2p-settings/block", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    BlockPeerRequest request) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null) return Results.NotFound();

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null)
    {
        settings = new UserP2PSettings { UserId = user.Id };
        db.UserP2PSettings.Add(settings);
    }

    var trustedList = ParsePeerList(settings.TrustedPeerIds);
    var blockedList = ParsePeerList(settings.BlockedPeerIds);

    // Remove from trusted if present
    trustedList.Remove(request.PeerId);

    // Add to blocked if not already present
    if (!blockedList.Contains(request.PeerId))
    {
        blockedList.Add(request.PeerId);
    }

    settings.TrustedPeerIds = System.Text.Json.JsonSerializer.Serialize(trustedList);
    settings.BlockedPeerIds = System.Text.Json.JsonSerializer.Serialize(blockedList);
    settings.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// Untrust a peer
app.MapDelete("/api/p2p-settings/trust/{peerId}", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    string peerId) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null) return Results.NotFound();

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null) return Results.NotFound();

    var trustedList = ParsePeerList(settings.TrustedPeerIds);
    trustedList.Remove(peerId);
    settings.TrustedPeerIds = System.Text.Json.JsonSerializer.Serialize(trustedList);
    settings.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// Unblock a peer
app.MapDelete("/api/p2p-settings/block/{peerId}", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    string peerId) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user == null) return Results.NotFound();

    var settings = await db.UserP2PSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
    if (settings == null) return Results.NotFound();

    var blockedList = ParsePeerList(settings.BlockedPeerIds);
    blockedList.Remove(peerId);
    settings.BlockedPeerIds = System.Text.Json.JsonSerializer.Serialize(blockedList);
    settings.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.Run();

// Helper to parse peer list from JSON
static List<string> ParsePeerList(string? json)
{
    if (string.IsNullOrEmpty(json)) return [];
    try
    {
        return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
    catch
    {
        return [];
    }
}

// DTOs for authentication
public record UserInfo(bool IsAuthenticated, string? Name, ClaimInfo[] Claims);
public record ClaimInfo(string Type, string Value);

// DTOs for profile
public record UserProfile(
    string? Email,
    string? UserName,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PhoneNumber,
    AddressInfo? Address,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AddressInfo(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);

public record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PhoneNumber,
    UpdateAddressRequest? Address
);

public record UpdateAddressRequest(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// DTOs for P2P settings
public record UserP2PSettingsResponse(
    string? PeerId,
    string? P2PDisplayName,
    string Status,
    string? StatusMessage,
    bool EnableLanDiscovery,
    bool EnableWanDiscovery,
    bool RequirePeerVerification,
    bool AutoAcceptTrustedPeers,
    bool EnablePluginSharing,
    bool AutoDownloadFromTrusted,
    bool RequireSignedPlugins,
    List<string> TrustedPeerIds,
    List<string> BlockedPeerIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateP2PSettingsRequest(
    string? P2PDisplayName,
    string? Status,
    string? StatusMessage,
    bool? EnableLanDiscovery,
    bool? EnableWanDiscovery,
    bool? RequirePeerVerification,
    bool? AutoAcceptTrustedPeers,
    bool? EnablePluginSharing,
    bool? AutoDownloadFromTrusted,
    bool? RequireSignedPlugins
);

public record TrustPeerRequest(string PeerId);
public record BlockPeerRequest(string PeerId);
