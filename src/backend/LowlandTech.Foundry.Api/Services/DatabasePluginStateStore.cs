using LowlandTech.Foundry.Api.Data;
using LowlandTech.Foundry.PluginCore.Abstractions;
using LowlandTech.Foundry.PluginCore.Services;
using Microsoft.EntityFrameworkCore;

namespace LowlandTech.Foundry.Api.Services;

/// <summary>
/// Database-backed implementation of IPluginStateStore using EF Core.
/// </summary>
public class DatabasePluginStateStore : IPluginStateStore
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public DatabasePluginStateStore(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PluginStateRecord?> GetPluginStateAsync(string pluginId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.PluginStates.FindAsync(pluginId);

        return entity == null ? null : MapToRecord(entity);
    }

    public async Task<IReadOnlyList<PluginStateRecord>> GetAllPluginStatesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.PluginStates.ToListAsync();

        return entities.Select(MapToRecord).ToList();
    }

    public async Task SavePluginStateAsync(PluginStateRecord record)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.PluginStates.FindAsync(record.PluginId);

        if (entity == null)
        {
            entity = new PluginStateEntity
            {
                PluginId = record.PluginId
            };
            context.PluginStates.Add(entity);
        }

        entity.State = record.State.ToString();
        entity.InstalledAt = record.InstalledAt;
        entity.ActivatedAt = record.ActivatedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeletePluginStateAsync(string pluginId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.PluginStates.FindAsync(pluginId);

        if (entity != null)
        {
            context.PluginStates.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<FeatureStateRecord?> GetFeatureStateAsync(string pluginId, string featureId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.FeatureStates
            .FirstOrDefaultAsync(f => f.PluginId == pluginId && f.FeatureId == featureId);

        return entity == null ? null : MapToRecord(entity);
    }

    public async Task<IReadOnlyList<FeatureStateRecord>> GetFeatureStatesAsync(string pluginId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.FeatureStates
            .Where(f => f.PluginId == pluginId)
            .ToListAsync();

        return entities.Select(MapToRecord).ToList();
    }

    public async Task SaveFeatureStateAsync(FeatureStateRecord record)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.FeatureStates
            .FirstOrDefaultAsync(f => f.PluginId == record.PluginId && f.FeatureId == record.FeatureId);

        if (entity == null)
        {
            entity = new FeatureStateEntity
            {
                PluginId = record.PluginId,
                FeatureId = record.FeatureId
            };
            context.FeatureStates.Add(entity);
        }

        entity.IsEnabled = record.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteFeatureStatesAsync(string pluginId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.FeatureStates
            .Where(f => f.PluginId == pluginId)
            .ToListAsync();

        if (entities.Count > 0)
        {
            context.FeatureStates.RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    private static PluginStateRecord MapToRecord(PluginStateEntity entity)
    {
        return new PluginStateRecord
        {
            PluginId = entity.PluginId,
            State = Enum.TryParse<PluginState>(entity.State, out var state) ? state : PluginState.Discovered,
            InstalledAt = entity.InstalledAt,
            ActivatedAt = entity.ActivatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static FeatureStateRecord MapToRecord(FeatureStateEntity entity)
    {
        return new FeatureStateRecord
        {
            PluginId = entity.PluginId,
            FeatureId = entity.FeatureId,
            IsEnabled = entity.IsEnabled,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
