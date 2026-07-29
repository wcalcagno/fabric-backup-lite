using Fabric_backup_lite.Models;

namespace Fabric_backup_lite.Services;

public interface IFabricApiClient
{
    Task<List<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<List<FabricItem>> GetWorkspaceItemsAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<List<(byte[] content, string partPath)>> GetItemDefinitionAsync(
        string workspaceId,
        string itemId,
        FabricItemType itemType,
        CancellationToken cancellationToken = default);

    // Workspace-level metadata worth capturing alongside item definitions.
    // Returns a map of { sidecarFileName => rawJson }; endpoints that fail (e.g. lacking
    // permission) are skipped rather than aborting the backup.
    Task<Dictionary<string, string>> GetWorkspaceMetadataJsonAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);

    // Restore operations
    Task<List<FabricCapacity>> GetCapacitiesAsync(CancellationToken cancellationToken = default);
    Task<Workspace> CreateWorkspaceAsync(string displayName, string capacityId, CancellationToken cancellationToken = default);
    Task<string> CreateItemWithDefinitionAsync(
        string workspaceId,
        FabricItemType itemType,
        string displayName,
        List<(byte[] content, string partPath)> parts,
        CancellationToken cancellationToken = default);
}
