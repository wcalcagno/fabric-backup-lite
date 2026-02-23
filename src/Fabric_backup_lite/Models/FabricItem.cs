namespace Fabric_backup_lite.Models;

public class FabricItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public FabricItemType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
}
