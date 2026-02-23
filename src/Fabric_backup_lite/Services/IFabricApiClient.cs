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
}
