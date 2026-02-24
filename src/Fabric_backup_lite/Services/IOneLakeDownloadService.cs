using Fabric_backup_lite.Models;

namespace Fabric_backup_lite.Services;

public interface IOneLakeDownloadService
{
    /// <summary>
    /// Returns true for item types that must be backed up via OneLake ADLS API
    /// (i.e. those without a getDefinition REST endpoint).
    /// </summary>
    bool RequiresOneLakeDownload(FabricItemType itemType);

    /// <summary>
    /// Downloads all files for the given item from OneLake to a local directory.
    /// Returns the number of files downloaded.
    /// </summary>
    Task<int> DownloadItemAsync(
        string workspaceId,
        FabricItem item,
        string backupRootPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
