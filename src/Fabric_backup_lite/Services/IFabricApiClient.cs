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
