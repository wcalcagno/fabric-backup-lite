using Fabric_backup_lite.Models;

namespace Fabric_backup_lite.Services;

public interface IRestoreService
{
    /// <summary>
    /// Scans <paramref name="rootFolder"/> recursively for manifest.json files.
    /// Returns one <see cref="BackupSummary"/> per discovered backup folder, sorted newest-first.
    /// </summary>
    Task<List<BackupSummary>> DiscoverBackupsAsync(
        string rootFolder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the manifest.json inside <paramref name="backupFolderPath"/> and returns a list of
    /// <see cref="RestoreItemViewModel"/> already annotated with IsRestorable and IsChecked defaults.
    /// </summary>
    Task<List<RestoreItemViewModel>> LoadManifestItemsAsync(
        string backupFolderPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores each selected item into <paramref name="targetWorkspaceId"/> by reading files from
    /// disk and calling the Fabric API createItemWithDefinition endpoint.
    /// Non-fatal per-item errors are collected and returned in <see cref="RestoreResult.Errors"/>.
    /// </summary>
    Task<RestoreResult> RestoreItemsAsync(
        string backupFolderPath,
        string targetWorkspaceId,
        string targetWorkspaceName,
        IList<RestoreItemViewModel> selectedItems,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
