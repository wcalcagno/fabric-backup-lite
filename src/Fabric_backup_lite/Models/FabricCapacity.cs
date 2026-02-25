namespace Fabric_backup_lite.Models;

public class FabricCapacity
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    public bool IsTrial => Sku.Equals("Trial", StringComparison.OrdinalIgnoreCase)
                        || Sku.Equals("FT1",   StringComparison.OrdinalIgnoreCase);

    public string DisplayLabel => IsTrial
        ? $"{DisplayName} (Trial — {Sku})"
        : $"{DisplayName} ({Sku}, {Region})";
}
