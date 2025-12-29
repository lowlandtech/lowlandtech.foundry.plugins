using LowlandTech.Foundry.Api.Data;
using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Hosting;
using LowlandTech.Foundry.PluginCore.Manifest;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace LowlandTech.Foundry.Api.Services;

/// <summary>
/// Plugin host that loads plugins directly into the API process.
/// Used for simpler deployments, mobile backends, and development.
/// </summary>
public class InProcessPluginHost : IPluginHost
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessPluginHost> _logger;
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly Dictionary<string, IPlugin> _loadedPlugins = new();

    public string HostingMode => PluginHostingMode.InProcess;

    public InProcessPluginHost(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IServiceProvider serviceProvider,
        ILogger<InProcessPluginHost> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<PluginInstallResult> InstallAsync(
        PluginManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var pluginId = manifest.Id;
        var schemaName = manifest.Persistence.SchemaName
            ?? $"plug_{pluginId.Replace(".", "_").ToLowerInvariant()}";

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Check if already installed
            var existing = await db.InstalledPluginPackages
                .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

            if (existing != null)
            {
                return new PluginInstallResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ErrorMessage = $"Plugin {pluginId} is already installed"
                };
            }

            // Create the database schema
            var appliedMigrations = await CreatePluginSchemaAsync(
                db, schemaName, manifest, cancellationToken);

            // Find the plugin assembly path
            var assemblyPath = FindPluginAssembly(packagePath, manifest);

            // Create the installed plugin record
            var installedPlugin = new InstalledPluginPackage
            {
                PluginId = pluginId,
                DisplayName = manifest.DisplayName,
                Version = manifest.Version,
                Description = manifest.Description,
                Author = manifest.Author,
                HostingMode = PluginHostingMode.InProcess,
                RouteBase = manifest.Runtime.RouteBase,
                SchemaName = schemaName,
                OpenApiManifest = System.Text.Json.JsonSerializer.Serialize(manifest),
                AssemblyPath = assemblyPath,
                EntryPointClass = manifest.Id, // Will be discovered from assembly
                HealthPath = manifest.Runtime.HealthPath,
                OpenApiPath = manifest.Runtime.OpenApiPath,
                HostApiCompatibility = manifest.HostApiCompatibility,
                MigrationsApplied = true,
                LastAppliedMigration = appliedMigrations.LastOrDefault(),
                RuntimeStatus = PluginRuntimeStatus.Stopped,
                InstalledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.InstalledPluginPackages.Add(installedPlugin);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Installed in-process plugin {PluginId} with schema {SchemaName}",
                pluginId, schemaName);

            return new PluginInstallResult
            {
                Success = true,
                PluginId = pluginId,
                SchemaName = schemaName,
                AppliedMigrations = appliedMigrations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install plugin {PluginId}", pluginId);
            return new PluginInstallResult
            {
                Success = false,
                PluginId = pluginId,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        bool removeData = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var plugin = await db.InstalledPluginPackages
                .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

            if (plugin == null)
            {
                return new PluginUninstallResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ErrorMessage = $"Plugin {pluginId} is not installed"
                };
            }

            // Unload the assembly if loaded
            if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                await loadedPlugin.DeactivateAsync(_serviceProvider);
                await loadedPlugin.UninstallAsync(_serviceProvider);
                _loadedPlugins.Remove(pluginId);
            }

            if (_loadContexts.TryGetValue(pluginId, out var loadContext))
            {
                loadContext.Unload();
                _loadContexts.Remove(pluginId);
            }

            // Drop the schema if requested
            var dataRemoved = false;
            if (removeData && !string.IsNullOrEmpty(plugin.SchemaName))
            {
                await DropPluginSchemaAsync(db, plugin.SchemaName, cancellationToken);
                dataRemoved = true;
            }

            // Remove the plugin record
            db.InstalledPluginPackages.Remove(plugin);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Uninstalled in-process plugin {PluginId}, data removed: {DataRemoved}",
                pluginId, dataRemoved);

            return new PluginUninstallResult
            {
                Success = true,
                PluginId = pluginId,
                DataRemoved = dataRemoved
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall plugin {PluginId}", pluginId);
            return new PluginUninstallResult
            {
                Success = false,
                PluginId = pluginId,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PluginActivationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var plugin = await db.InstalledPluginPackages
                .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

            if (plugin == null)
            {
                return new PluginActivationResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ErrorMessage = $"Plugin {pluginId} is not installed"
                };
            }

            if (_loadedPlugins.ContainsKey(pluginId))
            {
                return new PluginActivationResult
                {
                    Success = true,
                    PluginId = pluginId,
                    BaseUrl = GetPluginBaseUrl(pluginId),
                    StartupDuration = TimeSpan.Zero
                };
            }

            // Update status
            plugin.RuntimeStatus = PluginRuntimeStatus.Starting;
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Load the plugin assembly
            var loadedPlugin = await LoadPluginAssemblyAsync(plugin, cancellationToken);

            if (loadedPlugin == null)
            {
                plugin.RuntimeStatus = PluginRuntimeStatus.Error;
                plugin.ErrorMessage = "Failed to load plugin assembly";
                plugin.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                return new PluginActivationResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ErrorMessage = "Failed to load plugin assembly",
                    StartupDuration = stopwatch.Elapsed
                };
            }

            // Install and activate
            await loadedPlugin.InstallAsync(_serviceProvider);
            await loadedPlugin.ActivateAsync(_serviceProvider);

            _loadedPlugins[pluginId] = loadedPlugin;

            // Update status
            plugin.RuntimeStatus = PluginRuntimeStatus.Running;
            plugin.ActivatedAt = DateTime.UtcNow;
            plugin.UpdatedAt = DateTime.UtcNow;
            plugin.ErrorMessage = null;
            await db.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Activated in-process plugin {PluginId} in {Duration}ms",
                pluginId, stopwatch.ElapsedMilliseconds);

            return new PluginActivationResult
            {
                Success = true,
                PluginId = pluginId,
                BaseUrl = GetPluginBaseUrl(pluginId),
                StartupDuration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate plugin {PluginId}", pluginId);
            return new PluginActivationResult
            {
                Success = false,
                PluginId = pluginId,
                ErrorMessage = ex.Message,
                StartupDuration = stopwatch.Elapsed
            };
        }
    }

    public async Task<PluginDeactivationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var plugin = await db.InstalledPluginPackages
                .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

            if (plugin == null)
            {
                return new PluginDeactivationResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ErrorMessage = $"Plugin {pluginId} is not installed"
                };
            }

            // Deactivate the loaded plugin
            if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                await loadedPlugin.DeactivateAsync(_serviceProvider);
                _loadedPlugins.Remove(pluginId);
            }

            // Note: We don't unload the AssemblyLoadContext here as it may cause issues
            // with types still in use. Full unload happens on Uninstall.

            plugin.RuntimeStatus = PluginRuntimeStatus.Stopped;
            plugin.DeactivatedAt = DateTime.UtcNow;
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deactivated in-process plugin {PluginId}", pluginId);

            return new PluginDeactivationResult
            {
                Success = true,
                PluginId = pluginId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate plugin {PluginId}", pluginId);
            return new PluginDeactivationResult
            {
                Success = false,
                PluginId = pluginId,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PluginHealthResult> CheckHealthAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var plugin = await db.InstalledPluginPackages
                .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

            if (plugin == null)
            {
                return new PluginHealthResult
                {
                    IsHealthy = false,
                    PluginId = pluginId,
                    Status = "NotInstalled",
                    Message = "Plugin not installed"
                };
            }

            var isLoaded = _loadedPlugins.ContainsKey(pluginId);
            stopwatch.Stop();

            var isHealthy = isLoaded && plugin.RuntimeStatus == PluginRuntimeStatus.Running;

            // Update plugin record
            plugin.LastHealthCheck = DateTime.UtcNow;
            plugin.LastHealthStatus = isHealthy ? "Healthy" : "Unhealthy";
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return new PluginHealthResult
            {
                IsHealthy = isHealthy,
                PluginId = pluginId,
                Status = isHealthy ? "Healthy" : (isLoaded ? "Loaded" : "NotLoaded"),
                ResponseTime = stopwatch.Elapsed,
                Details = new Dictionary<string, object>
                {
                    ["isLoaded"] = isLoaded,
                    ["runtimeStatus"] = plugin.RuntimeStatus
                }
            };
        }
        catch (Exception ex)
        {
            return new PluginHealthResult
            {
                IsHealthy = false,
                PluginId = pluginId,
                Status = "Error",
                Message = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<PluginRuntimeInfo> GetRuntimeInfoAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var plugin = await db.InstalledPluginPackages
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

        if (plugin == null)
        {
            return new PluginRuntimeInfo
            {
                PluginId = pluginId,
                Status = "NotInstalled"
            };
        }

        var isLoaded = _loadedPlugins.ContainsKey(pluginId);

        return new PluginRuntimeInfo
        {
            PluginId = pluginId,
            Status = plugin.RuntimeStatus,
            HostingMode = plugin.HostingMode,
            BaseUrl = GetPluginBaseUrl(pluginId),
            StartedAt = plugin.ActivatedAt,
            Uptime = plugin.ActivatedAt.HasValue
                ? DateTime.UtcNow - plugin.ActivatedAt.Value
                : null
        };
    }

    public string GetPluginBaseUrl(string pluginId)
    {
        // In-process plugins use the same base path pattern
        return $"/api/plugins/{pluginId}";
    }

    // Private helper methods

    private async Task<List<string>> CreatePluginSchemaAsync(
        ApplicationDbContext db,
        string schemaName,
        PluginManifest manifest,
        CancellationToken cancellationToken)
    {
        var migrations = new List<string>();
        var sanitizedSchema = SanitizeSchemaName(schemaName);

#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA IF NOT EXISTS \"{sanitizedSchema}\"",
            cancellationToken);
#pragma warning restore EF1002

        migrations.Add($"CreateSchema_{sanitizedSchema}");

        _logger.LogInformation("Created schema {SchemaName} for plugin", sanitizedSchema);

        return migrations;
    }

    private async Task DropPluginSchemaAsync(
        ApplicationDbContext db,
        string schemaName,
        CancellationToken cancellationToken)
    {
        var sanitizedSchema = SanitizeSchemaName(schemaName);

#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync(
            $"DROP SCHEMA IF EXISTS \"{sanitizedSchema}\" CASCADE",
            cancellationToken);
#pragma warning restore EF1002

        _logger.LogInformation("Dropped schema {SchemaName}", schemaName);
    }

    private static string SanitizeSchemaName(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be empty", nameof(schemaName));

        var sanitized = new string(schemaName
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray());

        if (sanitized.Length == 0)
            throw new ArgumentException("Schema name contains no valid characters", nameof(schemaName));

        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        if (sanitized.Length > 63)
            sanitized = sanitized[..63];

        return sanitized.ToLowerInvariant();
    }

    private static string? FindPluginAssembly(string packagePath, PluginManifest manifest)
    {
        // Look for DLLs in lib/net10.0 or lib/net9.0 directories
        var libPaths = new[]
        {
            Path.Combine(packagePath, "lib", "net10.0"),
            Path.Combine(packagePath, "lib", "net9.0"),
            Path.Combine(packagePath, "lib", "netstandard2.1"),
            packagePath
        };

        foreach (var libPath in libPaths)
        {
            if (!Directory.Exists(libPath)) continue;

            var dlls = Directory.GetFiles(libPath, "*.dll");
            if (dlls.Length > 0)
            {
                // Return the first DLL (or try to find one matching the plugin ID)
                var matchingDll = dlls.FirstOrDefault(d =>
                    Path.GetFileNameWithoutExtension(d)
                        .Contains(manifest.Id.Split('.').Last(), StringComparison.OrdinalIgnoreCase));

                return matchingDll ?? dlls[0];
            }
        }

        return null;
    }

    private Task<IPlugin?> LoadPluginAssemblyAsync(
        InstalledPluginPackage plugin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(plugin.AssemblyPath) || !File.Exists(plugin.AssemblyPath))
        {
            _logger.LogError("Plugin assembly not found: {AssemblyPath}", plugin.AssemblyPath);
            return Task.FromResult<IPlugin?>(null);
        }

        try
        {
            // Create an isolated load context for this plugin
            var loadContext = new PluginLoadContext(plugin.AssemblyPath);
            _loadContexts[plugin.PluginId] = loadContext;

            // Load the assembly
            var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(plugin.AssemblyPath));
            var assembly = loadContext.LoadFromAssemblyName(assemblyName);

            // Find the IPlugin implementation
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogError("No IPlugin implementation found in {AssemblyPath}", plugin.AssemblyPath);
                return Task.FromResult<IPlugin?>(null);
            }

            // Create an instance
            var pluginInstance = (IPlugin?)Activator.CreateInstance(pluginType);

            if (pluginInstance != null)
            {
                // Update the entry point class
                plugin.EntryPointClass = pluginType.FullName;
            }

            return Task.FromResult(pluginInstance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin assembly {AssemblyPath}", plugin.AssemblyPath);
            return Task.FromResult<IPlugin?>(null);
        }
    }

    /// <summary>
    /// Custom AssemblyLoadContext for plugin isolation.
    /// </summary>
    private class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
