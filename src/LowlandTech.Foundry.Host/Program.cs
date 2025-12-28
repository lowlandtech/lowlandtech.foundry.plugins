using LowlandTech.Foundry.Host.Components;
using LowlandTech.Foundry.PluginCore.Extensions;
using LowlandTech.Foundry.PluginCore.Theming;
using LowlandTech.Foundry.PremiumTheme.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor
builder.Services.AddMudServices();

// Add plugin system - include assemblies from plugins
builder.Services.AddPluginSystem(
    typeof(LowlandTech.Foundry.SamplePlugin._Imports).Assembly
);

// Add theming with multiple themes
builder.Services.AddFoundryTheming(options =>
{
    options.AddTheme<DefaultTheme>();
    options.AddTheme<OceanTheme>();
    options.AddTheme<ForestTheme>();
    options.AddPremiumThemes(); // Add premium themes from plugin
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(LowlandTech.Foundry.SamplePlugin._Imports).Assembly);

app.Run();
