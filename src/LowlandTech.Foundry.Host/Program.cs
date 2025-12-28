using LowlandTech.Foundry.Host.Components;
using LowlandTech.Foundry.PluginCore.Extensions;
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
