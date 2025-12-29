using System.ComponentModel.DataAnnotations;

namespace LowlandTech.Foundry.Api.Data;

/// <summary>
/// Entity for persisting plugin state to the database.
/// </summary>
public class PluginStateEntity
{
    [Key]
    [MaxLength(256)]
    public string PluginId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string State { get; set; } = "Discovered";

    public DateTime InstalledAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
