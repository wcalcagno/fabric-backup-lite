namespace Fabric_backup_lite.Services;

public interface IFabCliService
{
    /// <summary>
    /// Returns true if the Fabric CLI (fab) is installed and accessible on PATH.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a single item to a local directory using the Fabric CLI.
    /// Returns the path to the exported item folder on success, null on failure.
    /// </summary>
    Task<string?> ExportItemAsync(
        string workspaceName,
        string itemName,
        string itemType,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
