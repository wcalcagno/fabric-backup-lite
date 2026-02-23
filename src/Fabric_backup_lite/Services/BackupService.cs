using System.IO;
using Fabric_backup_lite.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.Services;

public class BackupService : IBackupService
{
    private readonly IFabricApiClient _fabricClient;
    private readonly IAuthenticationService _authService;
    private readonly FileSystemService _fileSystem;
    private readonly ILogger<BackupService> _logger;
    private readonly HashSet<FabricItemType> _supportedTypes;

    public BackupService(
        IFabricApiClient fabricClient,
        IAuthenticationService authService,
        FileSystemService fileSystemService,
        IConfiguration configuration,
        ILogger<BackupService> logger)
    {
        _fabricClient = fabricClient;
        _authService = authService;
        _fileSystem = fileSystemService;
        _logger = logger;

        // Cargar tipos soportados de configuración
        var supportedTypesConfig = configuration.GetSection("Backup:SupportedItemTypes").Get<string[]>()
            ?? new[] { "Report", "SemanticModel", "Notebook", "DataPipeline", "Dataflow" };

        _supportedTypes = supportedTypesConfig
            .Select(t => Enum.TryParse<FabricItemType>(t, true, out var type) ? type : FabricItemType.Unknown)
            .Where(t => t != FabricItemType.Unknown)
            .ToHashSet();

        _logger.LogInformation("Backup service initialized with supported types: {Types}",
            string.Join(", ", _supportedTypes));
    }

    public async Task<BackupResult> BackupWorkspaceAsync(
        string workspaceId,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backup for workspace {WorkspaceId}", workspaceId);

        var result = new BackupResult();
        var backupItems = new List<BackupItemInfo>();

        try
        {
            // 1. Obtener workspace info
            var workspaces = await _fabricClient.GetWorkspacesAsync(cancellationToken);
            var workspace = workspaces.FirstOrDefault(w => w.Id == workspaceId);

            if (workspace == null)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} not found");
            }

            ReportProgress(progress, 0, 0, "", $"Found workspace: {workspace.Name}");

            // 2. Obtener tenant ID
            var tenantId = await _authService.GetTenantIdAsync();

            // 3. Crear estructura de directorios
            var backupPath = _fileSystem.CreateBackupDirectory(
                destinationPath,
                tenantId,
                workspaceId,
                workspace.Name);

            result.BackupPath = backupPath;
            ReportProgress(progress, 0, 0, "", $"Created backup directory: {backupPath}");

            // 4. Obtener items del workspace
            var allItems = await _fabricClient.GetWorkspaceItemsAsync(workspaceId, cancellationToken);

            // Filtrar solo tipos soportados
            var itemsToBackup = allItems.Where(i => _supportedTypes.Contains(i.Type)).ToList();

            if (itemsToBackup.Count == 0)
            {
                _logger.LogWarning("No supported items found in workspace");
                result.Success = true;
                return result;
            }

            _logger.LogInformation("Found {Total} items to backup ({All} total items)",
                itemsToBackup.Count, allItems.Count);

            ReportProgress(progress, itemsToBackup.Count, 0, "",
                $"Found {itemsToBackup.Count} items to backup");

            // 5. Backup cada item
            for (int i = 0; i < itemsToBackup.Count; i++)
            {
                var item = itemsToBackup[i];

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Backup cancelled by user");
                    result.Errors.Add("Backup cancelled by user");
                    break;
                }

                try
                {
                    ReportProgress(progress, itemsToBackup.Count, i,
                        item.DisplayName,
                        $"Backing up {item.Type}: {item.DisplayName}");

                    // Obtener definición del item
                    var parts = await _fabricClient.GetItemDefinitionAsync(
                        workspaceId,
                        item.Id,
                        item.Type,
                        cancellationToken);

                    // Guardar a disco
                    var relativePath = await _fileSystem.SaveItemDefinitionAsync(
                        backupPath,
                        item,
                        parts,
                        cancellationToken);

                    // Agregar al manifest
                    backupItems.Add(new BackupItemInfo
                    {
                        Type = item.Type.ToString(),
                        Id = item.Id,
                        Name = item.DisplayName,
                        RelativePath = relativePath
                    });

                    result.ItemsBackedUp++;

                    _logger.LogInformation("Successfully backed up {ItemName}", item.DisplayName);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning("Tipo no soportado, omitiendo {Item}: {Msg}", item.DisplayName, ex.Message);
                    ReportProgress(progress, itemsToBackup.Count, i + 1, item.DisplayName,
                        $"Omitido (tipo no soportado): {item.Type}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to backup {item.DisplayName}: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    result.Errors.Add(errorMsg);

                    ReportProgress(progress, itemsToBackup.Count, i + 1, item.DisplayName,
                        $"Error: {ex.Message}");
                }
            }

            // 6. Generar manifest.json
            var metadata = new BackupMetadata
            {
                Timestamp = DateTime.UtcNow,
                TenantId = tenantId,
                WorkspaceId = workspaceId,
                WorkspaceName = workspace.Name,
                Items = backupItems
            };

            await _fileSystem.SaveManifestAsync(backupPath, metadata, cancellationToken);

            // 7. Resultado final
            result.Success = result.Errors.Count == 0;

            var finalMessage = result.Success
                ? $"Backup completed successfully. {result.ItemsBackedUp} items backed up."
                : $"Backup completed with errors. {result.ItemsBackedUp} items backed up, {result.Errors.Count} errors.";

            ReportProgress(progress, itemsToBackup.Count, itemsToBackup.Count, "", finalMessage);

            _logger.LogInformation("Backup finished: {Success}, Items: {Count}, Errors: {Errors}",
                result.Success, result.ItemsBackedUp, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed with exception");
            result.Success = false;
            result.Errors.Add($"Critical error: {ex.Message}");
            return result;
        }
    }

    public async Task<BackupResult> BackupSelectedItemsAsync(
        IList<(string workspaceId, string workspaceName, FabricItem item)> selectedItems,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (selectedItems.Count == 0)
        {
            return new BackupResult
            {
                Success = false,
                Errors  = { "No hay ítems seleccionados para hacer backup." }
            };
        }

        var result   = new BackupResult();
        var tenantId = await _authService.GetTenantIdAsync();
        int total    = selectedItems.Count(i => _supportedTypes.Contains(i.item.Type));
        int completed = 0;

        ReportProgress(progress, total, 0, "", $"Iniciando backup de {total} ítem(s)...");

        // Agrupar por workspace
        var byWorkspace = selectedItems
            .Where(i => _supportedTypes.Contains(i.item.Type))
            .GroupBy(i => (i.workspaceId, i.workspaceName))
            .ToList();

        foreach (var wsGroup in byWorkspace)
        {
            var (workspaceId, workspaceName) = wsGroup.Key;
            var backupPath  = _fileSystem.CreateBackupDirectory(destinationPath, tenantId, workspaceId, workspaceName);
            var backupItems = new List<BackupItemInfo>();

            if (string.IsNullOrEmpty(result.BackupPath))
                result.BackupPath = backupPath;

            foreach (var (_, _, item) in wsGroup)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Errors.Add("Backup cancelado por el usuario.");
                    break;
                }

                try
                {
                    ReportProgress(progress, total, completed, item.DisplayName,
                        $"Guardando {item.Type}: {item.DisplayName}");

                    var parts = await _fabricClient.GetItemDefinitionAsync(
                        workspaceId, item.Id, item.Type, cancellationToken);

                    var relativePath = await _fileSystem.SaveItemDefinitionAsync(backupPath, item, parts, cancellationToken);

                    backupItems.Add(new BackupItemInfo
                    {
                        Type         = item.Type.ToString(),
                        Id           = item.Id,
                        Name         = item.DisplayName,
                        RelativePath = relativePath
                    });

                    result.ItemsBackedUp++;
                    completed++;
                    _logger.LogInformation("Backed up {Item}", item.DisplayName);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning("Tipo no soportado, omitiendo {Item}: {Msg}", item.DisplayName, ex.Message);
                    completed++;
                }
                catch (Exception ex)
                {
                    var err = $"Error al guardar {item.DisplayName}: {ex.Message}";
                    _logger.LogError(ex, err);
                    result.Errors.Add(err);
                    completed++;
                }
            }

            var metadata = new BackupMetadata
            {
                Timestamp     = DateTime.UtcNow,
                TenantId      = tenantId,
                WorkspaceId   = workspaceId,
                WorkspaceName = workspaceName,
                Items         = backupItems
            };

            await _fileSystem.SaveManifestAsync(backupPath, metadata, cancellationToken);
        }

        result.Success = result.Errors.Count == 0;

        var finalMsg = result.Success
            ? $"Backup completado: {result.ItemsBackedUp} ítem(s) guardado(s)."
            : $"Backup con errores: {result.ItemsBackedUp} guardado(s), {result.Errors.Count} error(es).";

        ReportProgress(progress, total, total, "", finalMsg);
        _logger.LogInformation("BackupSelectedItems finished: {Success}, Items: {Count}", result.Success, result.ItemsBackedUp);

        return result;
    }

    private void ReportProgress(
        IProgress<BackupProgress>? progress,
        int total,
        int completed,
        string currentItem,
        string message)
    {
        progress?.Report(new BackupProgress
        {
            TotalItems = total,
            CompletedItems = completed,
            CurrentItem = currentItem,
            Message = message
        });
    }

}
