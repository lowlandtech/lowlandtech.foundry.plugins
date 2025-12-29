using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LowlandTech.Foundry.Api.Data;

/// <summary>
/// Entity for persisting plugin feature state to the database.
/// </summary>
[PrimaryKey(nameof(PluginId), nameof(FeatureId))]
public class FeatureStateEntity
{
    [MaxLength(256)]
    public string PluginId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string FeatureId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }
}
