using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using MsLogger = Microsoft.Extensions.Logging.ILogger;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MsLoggerExtensions = Microsoft.Extensions.Logging.LoggerExtensions;

namespace LowlandTech.Foundry.PluginCore.Catalogs;

/// <summary>
/// Plugin catalog for loading plugins from NuGet packages
/// </summary>
public class NuGetPluginCatalog : IPluginCatalog
{
    private readonly string _packageId;
    private readonly string? _packageVersion;
    private readonly NuGetPluginCatalogOptions _options;
    private readonly MsLogger? _logger;
    private readonly List<AssemblyPluginCatalog> _catalogs = [];

    public bool IsInitialized { get; private set; }
    public string PackagesFolder { get; }

    public NuGetPluginCatalog(
        string packageId,
        string? packageVersion = null,
        NuGetPluginCatalogOptions? options = null,
        MsLogger? logger = null)
    {
        _packageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
        _packageVersion = packageVersion;
        _options = options ?? new NuGetPluginCatalogOptions();
        _logger = logger;

        PackagesFolder = _options.PackagesFolder
            ?? Path.Combine(Path.GetTempPath(), "FoundryPlugins", "packages");

        if (!Directory.Exists(PackagesFolder))
        {
            Directory.CreateDirectory(PackagesFolder);
        }
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        try
        {
            var assemblyPaths = await DownloadPackageAsync();

            foreach (var assemblyPath in assemblyPaths)
            {
                try
                {
                    var catalog = new AssemblyPluginCatalog(
                        assemblyPath,
                        _options.TypeFinderCriteria,
                        _options.PluginLoadContextOptions,
                        _logger);

                    await catalog.InitializeAsync();

                    if (catalog.GetPlugins().Count > 0)
                    {
                        _catalogs.Add(catalog);
                        if (_logger != null)
                            MsLoggerExtensions.LogInformation(_logger, "Loaded NuGet plugin: {PackageId} from {Path} with {Count} plugins",
                                _packageId, assemblyPath, catalog.GetPlugins().Count);
                    }
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        MsLoggerExtensions.LogWarning(_logger, ex, "Failed to load plugin from NuGet package assembly: {Path}", assemblyPath);
                }
            }
        }
        catch (Exception ex)
        {
            if (_logger != null)
                MsLoggerExtensions.LogError(_logger, ex, "Failed to download NuGet package: {PackageId}", _packageId);
        }

        IsInitialized = true;
    }

    public IReadOnlyList<PluginInfo> GetPlugins()
    {
        return _catalogs.SelectMany(c => c.GetPlugins()).ToList();
    }

    public PluginInfo? Get(string name, Version? version = null)
    {
        foreach (var catalog in _catalogs)
        {
            var plugin = catalog.Get(name, version);
            if (plugin != null)
            {
                return plugin;
            }
        }
        return null;
    }

    private async Task<List<string>> DownloadPackageAsync()
    {
        var assemblyPaths = new List<string>();
        var nugetLogger = new NuGetLogger(_logger);

        // Set up package sources
        var sources = new List<PackageSource>();

        if (_options.PackageSources?.Count > 0)
        {
            foreach (var source in _options.PackageSources)
            {
                sources.Add(new PackageSource(source));
            }
        }
        else
        {
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
        }

        var targetFramework = NuGetFramework.Parse(_options.TargetFramework ?? "net10.0");
        var cache = new SourceCacheContext();

        foreach (var packageSource in sources)
        {
            try
            {
                var repository = Repository.Factory.GetCoreV3(packageSource);
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

                // Determine version to download
                NuGetVersion? versionToDownload = null;

                if (!string.IsNullOrEmpty(_packageVersion))
                {
                    versionToDownload = NuGetVersion.Parse(_packageVersion);
                }
                else
                {
                    var versions = await resource.GetAllVersionsAsync(_packageId, cache, nugetLogger, CancellationToken.None);
                    versionToDownload = versions
                        .Where(v => _options.IncludePrerelease || !v.IsPrerelease)
                        .OrderByDescending(v => v)
                        .FirstOrDefault();
                }

                if (versionToDownload == null)
                {
                    if (_logger != null)
                        MsLoggerExtensions.LogWarning(_logger, "No suitable version found for package {PackageId}", _packageId);
                    continue;
                }

                if (_logger != null)
                    MsLoggerExtensions.LogDebug(_logger, "Downloading package {PackageId} version {Version}", _packageId, versionToDownload);

                // Download the package
                var packagePath = Path.Combine(PackagesFolder, $"{_packageId}.{versionToDownload}");

                if (!Directory.Exists(packagePath))
                {
                    Directory.CreateDirectory(packagePath);

                    using var packageStream = new MemoryStream();
                    await resource.CopyNupkgToStreamAsync(
                        _packageId,
                        versionToDownload,
                        packageStream,
                        cache,
                        nugetLogger,
                        CancellationToken.None);

                    packageStream.Seek(0, SeekOrigin.Begin);

                    using var packageReader = new PackageArchiveReader(packageStream);

                    // Extract lib files for the target framework
                    var libItems = packageReader.GetLibItems().ToList();
                    var bestFramework = NuGetFrameworkUtility.GetNearest(libItems, targetFramework);

                    if (bestFramework != null)
                    {
                        foreach (var file in bestFramework.Items)
                        {
                            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                var targetPath = Path.Combine(packagePath, Path.GetFileName(file));
                                using var entryStream = packageReader.GetStream(file);
                                using var fileStream = File.Create(targetPath);
                                await entryStream.CopyToAsync(fileStream);

                                assemblyPaths.Add(targetPath);
                            }
                        }
                    }
                }
                else
                {
                    // Package already downloaded, find the assemblies
                    var dllFiles = Directory.GetFiles(packagePath, "*.dll");
                    assemblyPaths.AddRange(dllFiles);
                }

                break; // Successfully downloaded from this source
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    MsLoggerExtensions.LogDebug(_logger, ex, "Failed to download from source {Source}", packageSource.Source);
            }
        }

        return assemblyPaths;
    }
}

/// <summary>
/// Options for the NuGetPluginCatalog
/// </summary>
public class NuGetPluginCatalogOptions
{
    /// <summary>
    /// Folder to store downloaded packages
    /// </summary>
    public string? PackagesFolder { get; set; }

    /// <summary>
    /// NuGet package sources (defaults to nuget.org)
    /// </summary>
    public List<string>? PackageSources { get; set; }

    /// <summary>
    /// Whether to include prerelease versions
    /// </summary>
    public bool IncludePrerelease { get; set; }

    /// <summary>
    /// Target framework for selecting assemblies (e.g., "net10.0")
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Criteria for finding plugin types
    /// </summary>
    public TypeFinderCriteria? TypeFinderCriteria { get; set; }

    /// <summary>
    /// Options for the plugin load context
    /// </summary>
    public PluginLoadContextOptions PluginLoadContextOptions { get; set; } = new();
}

/// <summary>
/// NuGet logger adapter
/// </summary>
internal class NuGetLogger : ILogger
{
    private readonly MsLogger? _logger;

    public NuGetLogger(MsLogger? logger)
    {
        _logger = logger;
    }

    public void LogDebug(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogDebug(_logger, data);
    }

    public void LogVerbose(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogTrace(_logger, data);
    }

    public void LogInformation(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogInformation(_logger, data);
    }

    public void LogMinimal(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogInformation(_logger, data);
    }

    public void LogWarning(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogWarning(_logger, data);
    }

    public void LogError(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogError(_logger, data);
    }

    public void LogInformationSummary(string data)
    {
        if (_logger != null) MsLoggerExtensions.LogInformation(_logger, data);
    }

    public void Log(LogLevel level, string data)
    {
        if (_logger == null) return;

        var msLevel = ToMicrosoftLogLevel(level);
        MsLoggerExtensions.Log(_logger, msLevel, "{Message}", data);
    }

    public Task LogAsync(LogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message) => Log(message.Level, message.Message);
    public Task LogAsync(ILogMessage message) { Log(message); return Task.CompletedTask; }

    private static MsLogLevel ToMicrosoftLogLevel(LogLevel level) => level switch
    {
        LogLevel.Debug => MsLogLevel.Debug,
        LogLevel.Verbose => MsLogLevel.Trace,
        LogLevel.Information => MsLogLevel.Information,
        LogLevel.Minimal => MsLogLevel.Information,
        LogLevel.Warning => MsLogLevel.Warning,
        LogLevel.Error => MsLogLevel.Error,
        _ => MsLogLevel.Information
    };
}
