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

app.Run();

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
