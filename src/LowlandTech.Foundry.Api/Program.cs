using LowlandTech.Foundry.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add PostgreSQL with Aspire integration
builder.AddNpgsqlDbContext<ApplicationDbContext>("identitydb");

// Add Identity services with cookie authentication
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
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
app.MapIdentityApi<IdentityUser>();

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
app.MapPost("/api/auth/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok();
}).RequireAuthorization();

app.Run();

// DTOs for authentication
public record UserInfo(bool IsAuthenticated, string? Name, ClaimInfo[] Claims);
public record ClaimInfo(string Type, string Value);
