using System.Text.Json.Serialization;

namespace Fabric_backup_lite.Models;

public class Workspace
{
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CapacityId { get; set; } = string.Empty;
}
