using LowlandTech.Foundry.Host.Components;
using LowlandTech.Foundry.Host.Services;
using LowlandTech.Foundry.PluginCore.Extensions;
using LowlandTech.Foundry.PluginCore.Theming;
using LowlandTech.Foundry.PremiumTheme.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

// Make IThemeStorageProvider available
using IThemeStorageProvider = LowlandTech.Foundry.PluginCore.Theming.IThemeStorageProvider;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor
builder.Services.AddMudServices();

// Add plugin system with dynamic loading from configuration
// The SamplePlugin assembly is included for backward compatibility
// Additional plugins can be loaded from folders or NuGet via appsettings.json
builder.Services.AddPluginSystem(
    builder.Configuration,
    typeof(LowlandTech.Foundry.SamplePlugin._Imports).Assembly
);

// Add theme storage provider for persistence
builder.Services.AddScoped<IThemeStorageProvider, ThemeStorageProvider>();

// Add theming with multiple themes
builder.Services.AddFoundryTheming(options =>
{
    options.AddTheme<DefaultTheme>();
    options.AddTheme<OceanTheme>();
    options.AddTheme<ForestTheme>();
    options.AddPremiumThemes(); // Add premium themes from plugin
});

// Configure HttpClient for API communication with service discovery
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

// Add authentication services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<ApiAuthenticationStateProvider>());

// Add P2P settings service
builder.Services.AddScoped<IP2PSettingsService, P2PSettingsService>();

// Add authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Map service default endpoints (health checks)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(LowlandTech.Foundry.SamplePlugin._Imports).Assembly,
        typeof(LowlandTech.Foundry.Collaboration.UI.Components.ChatWindow).Assembly);

app.Run();
