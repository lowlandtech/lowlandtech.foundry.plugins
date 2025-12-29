using Docker.DotNet;
using Docker.DotNet.Models;
using LowlandTech.Foundry.Api.Data;
using LowlandTech.Foundry.PluginCore.Hosting;
using LowlandTech.Foundry.PluginCore.Manifest;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace LowlandTech.Foundry.Api.Services;

/// <summary>
/// Plugin host that runs plugins in isolated Docker containers.
/// Each plugin gets its own container with a reverse proxy route.
/// </summary>
public class ContainerPluginHost : IPluginHost, IDisposable
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<ContainerPluginHost> _logger;
    private readonly DockerClient _dockerClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public string HostingMode => PluginHostingMode.Container;

    public ContainerPluginHost(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<ContainerPluginHost> logger,
        IHttpClientFactory httpClientFactory)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        // Connect to Docker daemon
        _dockerClient = new DockerClientConfiguration().CreateClient();
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

            // Create the database schema for the plugin
            var appliedMigrations = await CreatePluginSchemaAsync(
                db, schemaName, manifest, cancellationToken);

            // Build Docker image for the plugin
            var imageName = await BuildPluginImageAsync(
                manifest, packagePath, cancellationToken);

            // Create the installed plugin record
            var installedPlugin = new InstalledPluginPackage
            {
                PluginId = pluginId,
                DisplayName = manifest.DisplayName,
                Version = manifest.Version,
                Description = manifest.Description,
                Author = manifest.Author,
                HostingMode = PluginHostingMode.Container,
                RouteBase = manifest.Runtime.RouteBase,
                SchemaName = schemaName,
                OpenApiManifest = System.Text.Json.JsonSerializer.Serialize(manifest),
                DockerImage = imageName,
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
                "Installed plugin {PluginId} with schema {SchemaName}",
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

            // Stop the container if running
            if (plugin.RuntimeStatus == PluginRuntimeStatus.Running && plugin.ContainerId != null)
            {
                await StopContainerAsync(plugin.ContainerId, cancellationToken);
            }

            // Remove the container
            if (plugin.ContainerId != null)
            {
                await RemoveContainerAsync(plugin.ContainerId, cancellationToken);
            }

            // Remove the Docker image
            if (plugin.DockerImage != null)
            {
                await RemoveImageAsync(plugin.DockerImage, cancellationToken);
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
                "Uninstalled plugin {PluginId}, data removed: {DataRemoved}",
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

            if (plugin.RuntimeStatus == PluginRuntimeStatus.Running)
            {
                return new PluginActivationResult
                {
                    Success = true,
                    PluginId = pluginId,
                    ContainerId = plugin.ContainerId,
                    BaseUrl = GetPluginBaseUrl(pluginId),
                    StartupDuration = TimeSpan.Zero
                };
            }

            // Get hosting settings
            var settings = await db.PluginHostingSettings.FirstOrDefaultAsync(cancellationToken)
                ?? new PluginHostingSettings();

            // Update status
            plugin.RuntimeStatus = PluginRuntimeStatus.Starting;
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Start or create container
            var containerId = plugin.ContainerId;
            if (containerId != null)
            {
                // Restart existing container
                await _dockerClient.Containers.StartContainerAsync(
                    containerId,
                    new ContainerStartParameters(),
                    cancellationToken);
            }
            else
            {
                // Create new container
                containerId = await CreateAndStartContainerAsync(
                    plugin, settings, cancellationToken);
                plugin.ContainerId = containerId;
            }

            // Wait for health check
            var healthy = await WaitForHealthyAsync(
                plugin, settings.ContainerStartupTimeoutSeconds, cancellationToken);

            if (!healthy)
            {
                plugin.RuntimeStatus = PluginRuntimeStatus.Unhealthy;
                plugin.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                return new PluginActivationResult
                {
                    Success = false,
                    PluginId = pluginId,
                    ContainerId = containerId,
                    ErrorMessage = "Container started but health check failed",
                    StartupDuration = stopwatch.Elapsed
                };
            }

            // Update status
            plugin.RuntimeStatus = PluginRuntimeStatus.Running;
            plugin.ActivatedAt = DateTime.UtcNow;
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Activated plugin {PluginId} in {Duration}ms, container {ContainerId}",
                pluginId, stopwatch.ElapsedMilliseconds, containerId);

            return new PluginActivationResult
            {
                Success = true,
                PluginId = pluginId,
                ContainerId = containerId,
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

            if (plugin.ContainerId != null)
            {
                plugin.RuntimeStatus = PluginRuntimeStatus.Stopping;
                plugin.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                await StopContainerAsync(plugin.ContainerId, cancellationToken);
            }

            plugin.RuntimeStatus = PluginRuntimeStatus.Stopped;
            plugin.DeactivatedAt = DateTime.UtcNow;
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deactivated plugin {PluginId}", pluginId);

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

            if (plugin == null || plugin.RuntimeStatus != PluginRuntimeStatus.Running)
            {
                return new PluginHealthResult
                {
                    IsHealthy = false,
                    PluginId = pluginId,
                    Status = plugin?.RuntimeStatus ?? "NotInstalled",
                    Message = plugin == null ? "Plugin not installed" : "Plugin not running"
                };
            }

            // Call health endpoint
            var healthUrl = $"{GetPluginBaseUrl(pluginId)}{plugin.HealthPath}";
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync(healthUrl, cancellationToken);
            stopwatch.Stop();

            var isHealthy = response.IsSuccessStatusCode;

            // Update plugin record
            plugin.LastHealthCheck = DateTime.UtcNow;
            plugin.LastHealthStatus = isHealthy ? "Healthy" : "Unhealthy";
            if (!isHealthy)
            {
                plugin.RuntimeStatus = PluginRuntimeStatus.Unhealthy;
            }
            plugin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return new PluginHealthResult
            {
                IsHealthy = isHealthy,
                PluginId = pluginId,
                Status = isHealthy ? "Healthy" : "Unhealthy",
                ResponseTime = stopwatch.Elapsed
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

        var info = new PluginRuntimeInfo
        {
            PluginId = pluginId,
            Status = plugin.RuntimeStatus,
            HostingMode = plugin.HostingMode,
            ContainerId = plugin.ContainerId,
            BaseUrl = GetPluginBaseUrl(pluginId),
            StartedAt = plugin.ActivatedAt
        };

        // Get container stats if running
        if (plugin.ContainerId != null && plugin.RuntimeStatus == PluginRuntimeStatus.Running)
        {
            try
            {
                var stats = await GetContainerStatsAsync(plugin.ContainerId, cancellationToken);
                info = info with
                {
                    MemoryUsageBytes = stats.MemoryUsage,
                    CpuUsagePercent = stats.CpuPercent,
                    Uptime = plugin.ActivatedAt.HasValue
                        ? DateTime.UtcNow - plugin.ActivatedAt.Value
                        : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get stats for container {ContainerId}",
                    plugin.ContainerId);
            }
        }

        return info;
    }

    public string GetPluginBaseUrl(string pluginId)
    {
        // The reverse proxy routes to containers based on this pattern
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

        // Validate schema name to prevent SQL injection (only alphanumeric and underscore)
        var sanitizedSchema = SanitizeSchemaName(schemaName);

        // Create the schema - using FormattableString for proper escaping
#pragma warning disable EF1002 // Schema names cannot be parameterized, but we sanitize the input
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA IF NOT EXISTS \"{sanitizedSchema}\"",
            cancellationToken);
#pragma warning restore EF1002

        migrations.Add($"CreateSchema_{sanitizedSchema}");

        // TODO: Parse manifest entities and create tables
        // This will be handled by the code generator

        _logger.LogInformation("Created schema {SchemaName} for plugin", sanitizedSchema);

        return migrations;
    }

    private async Task DropPluginSchemaAsync(
        ApplicationDbContext db,
        string schemaName,
        CancellationToken cancellationToken)
    {
        var sanitizedSchema = SanitizeSchemaName(schemaName);

#pragma warning disable EF1002 // Schema names cannot be parameterized, but we sanitize the input
        await db.Database.ExecuteSqlRawAsync(
            $"DROP SCHEMA IF EXISTS \"{sanitizedSchema}\" CASCADE",
            cancellationToken);
#pragma warning restore EF1002

        _logger.LogInformation("Dropped schema {SchemaName}", schemaName);
    }

    private async Task<string> BuildPluginImageAsync(
        PluginManifest manifest,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var imageName = $"foundry-plugin-{manifest.Id}:{manifest.Version}".ToLowerInvariant();

        // TODO: Generate Dockerfile and build image
        // For now, just return the image name
        _logger.LogInformation("Would build Docker image {ImageName} from {PackagePath}",
            imageName, packagePath);

        return imageName;
    }

    private async Task<string> CreateAndStartContainerAsync(
        InstalledPluginPackage plugin,
        PluginHostingSettings settings,
        CancellationToken cancellationToken)
    {
        var containerName = $"plugin-{plugin.PluginId}".ToLowerInvariant().Replace(".", "-");

        // Create container
        var response = await _dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = plugin.DockerImage,
                Name = containerName,
                Env = new List<string>
                {
                    $"ASPNETCORE_URLS=http://+:80",
                    $"ConnectionStrings__DefaultConnection=Host=host.docker.internal;Database=foundry;Schema={plugin.SchemaName}",
                    $"Plugin__Id={plugin.PluginId}",
                    $"Plugin__RouteBase={plugin.RouteBase}"
                },
                HostConfig = new HostConfig
                {
                    Memory = settings.DefaultContainerMemoryMb * 1024 * 1024,
                    CPUPercent = (long)(double.Parse(settings.DefaultContainerCpuLimit) * 100),
                    NetworkMode = settings.DockerNetwork,
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                    PublishAllPorts = true
                },
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { "80/tcp", default }
                },
                Labels = new Dictionary<string, string>
                {
                    { "foundry.plugin.id", plugin.PluginId },
                    { "foundry.plugin.version", plugin.Version }
                }
            },
            cancellationToken);

        // Start container
        await _dockerClient.Containers.StartContainerAsync(
            response.ID,
            new ContainerStartParameters(),
            cancellationToken);

        _logger.LogInformation(
            "Created and started container {ContainerId} for plugin {PluginId}",
            response.ID, plugin.PluginId);

        return response.ID;
    }

    private async Task StopContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 10 },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // Container already removed
        }
    }

    private async Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed
        }
    }

    private async Task RemoveImageAsync(string imageName, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerClient.Images.DeleteImageAsync(
                imageName,
                new ImageDeleteParameters { Force = true },
                cancellationToken);
        }
        catch (DockerImageNotFoundException)
        {
            // Already removed
        }
    }

    private async Task<bool> WaitForHealthyAsync(
        InstalledPluginPackage plugin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var healthUrl = $"http://localhost{plugin.RouteBase}{plugin.HealthPath}";
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    private async Task<(long MemoryUsage, double CpuPercent)> GetContainerStatsAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // Using older API for simplicity
        var stats = await _dockerClient.Containers.GetContainerStatsAsync(
            containerId,
            new ContainerStatsParameters { Stream = false },
            cancellationToken);
#pragma warning restore CS0618

        // Docker stats come as a stream, we just want one reading
        // For now, return placeholder values
        return (0, 0);
    }

    /// <summary>
    /// Sanitizes a schema name to prevent SQL injection.
    /// Only allows alphanumeric characters and underscores.
    /// </summary>
    private static string SanitizeSchemaName(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be empty", nameof(schemaName));

        // Only allow alphanumeric and underscore
        var sanitized = new string(schemaName
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray());

        if (sanitized.Length == 0)
            throw new ArgumentException("Schema name contains no valid characters", nameof(schemaName));

        // Ensure it starts with a letter or underscore (PostgreSQL requirement)
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Limit length (PostgreSQL max identifier is 63 chars)
        if (sanitized.Length > 63)
            sanitized = sanitized[..63];

        return sanitized.ToLowerInvariant();
    }

    public void Dispose()
    {
        _dockerClient.Dispose();
    }
}
