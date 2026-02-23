using Fabric_backup_lite.Models;

namespace Fabric_backup_lite.Services;

public interface IBackupService
{
    Task<BackupResult> BackupWorkspaceAsync(
        string workspaceId,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<BackupResult> BackupSelectedItemsAsync(
        IList<(string workspaceId, string workspaceName, FabricItem item)> selectedItems,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
