using System.IO;
using System.Text.Json;
using Fabric_backup_lite.Models;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.Services;

public class RestoreService : IRestoreService
{
    private readonly IFabricApiClient _fabricClient;
    private readonly ILogger<RestoreService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public RestoreService(IFabricApiClient fabricClient, ILogger<RestoreService> logger)
    {
        _fabricClient = fabricClient;
        _logger       = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // DiscoverBackupsAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<List<BackupSummary>> DiscoverBackupsAsync(
        string rootFolder,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering backups under: {Root}", rootFolder);

        if (!Directory.Exists(rootFolder))
            return new List<BackupSummary>();

        var manifestPaths = Directory.GetFiles(rootFolder, "manifest.json", SearchOption.AllDirectories);
        var summaries     = new List<BackupSummary>();

        foreach (var manifestPath in manifestPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json     = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<BackupMetadata>(json, _jsonOpts);
                if (metadata is null) continue;

                var backupFolder = Path.GetDirectoryName(manifestPath)!;
                var label        = $"{metadata.WorkspaceName}  —  " +
                                   $"{metadata.Timestamp:yyyy-MM-dd HH:mm}  " +
                                   $"({metadata.Items.Count} items)";

                summaries.Add(new BackupSummary
                {
                    BackupFolderPath = backupFolder,
                    Metadata         = metadata,
                    DisplayLabel     = label
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse manifest at {Path}", manifestPath);
            }
        }

        summaries.Sort((a, b) =>
            b.Metadata.Timestamp.CompareTo(a.Metadata.Timestamp));

        _logger.LogInformation("Found {Count} backup(s)", summaries.Count);
        return summaries;
    }

    // ──────────────────────────────────────────────────────────────
    // LoadManifestItemsAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<List<RestoreItemViewModel>> LoadManifestItemsAsync(
        string backupFolderPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading manifest from: {Path}", backupFolderPath);

        var manifestPath = Path.Combine(backupFolderPath, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("manifest.json not found", manifestPath);

        var json     = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<BackupMetadata>(json, _jsonOpts)
            ?? throw new InvalidOperationException("Could not parse manifest.json");

        var items = new List<RestoreItemViewModel>();

        foreach (var backupItem in metadata.Items)
        {
            var type        = ParseItemType(backupItem.Type);
            var isRestorable = type != FabricItemType.Warehouse && type != FabricItemType.Unknown;

            items.Add(new RestoreItemViewModel
            {
                Item        = backupItem,
                ItemType    = type,
                IsRestorable = isRestorable,
                IsChecked   = isRestorable,          // pre-check restoreable items
                TypeIcon    = GetItemIcon(type),
                TooltipText = isRestorable
                    ? string.Empty
                    : LocalizationService.Instance.WarehouseRestoreTooltip
            });
        }

        _logger.LogInformation("Loaded {Count} item(s) from manifest", items.Count);
        return items;
    }

    // ──────────────────────────────────────────────────────────────
    // RestoreItemsAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<RestoreResult> RestoreItemsAsync(
        string backupFolderPath,
        string targetWorkspaceId,
        string targetWorkspaceName,
        IList<RestoreItemViewModel> selectedItems,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new RestoreResult
        {
            DestinationWorkspaceId   = targetWorkspaceId,
            DestinationWorkspaceName = targetWorkspaceName
        };

        var total     = selectedItems.Count;
        var completed = 0;

        foreach (var vm in selectedItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemName = vm.Item.Name;
            var itemType = vm.ItemType;

            progress?.Report(new RestoreProgress
            {
                TotalItems     = total,
                CompletedItems = completed,
                CurrentItem    = itemName,
                Message        = string.Format(
                    LocalizationService.Instance.RestoringItemFmt,
                    itemType, itemName)
            });

            try
            {
                var parts = await ReadItemPartsAsync(backupFolderPath, vm, cancellationToken);

                var newId = await _fabricClient.CreateItemWithDefinitionAsync(
                    targetWorkspaceId, itemType, itemName, parts, cancellationToken);

                _logger.LogInformation("Restored {Type} '{Name}' → new ID {Id}", itemType, itemName, newId);
                result.ItemsRestored++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format(
                    LocalizationService.Instance.ErrorSavingItemFmt,
                    itemName, ex.Message);
                _logger.LogError(ex, "Error restoring {Type} '{Name}'", itemType, itemName);
                result.Errors.Add(errorMsg);
            }

            completed++;
        }

        result.Success = result.Errors.Count == 0;
        return result;
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all files under the item's subfolder and returns them as (bytes, relativePath) tuples.
    /// This is the exact reverse of FileSystemService.SaveItemDefinitionAsync.
    /// For Lakehouse items, returns an empty list (the lakehouse is created empty).
    /// </summary>
    private async Task<List<(byte[] content, string partPath)>> ReadItemPartsAsync(
        string backupFolderPath,
        RestoreItemViewModel vm,
        CancellationToken cancellationToken)
    {
        if (vm.ItemType == FabricItemType.Lakehouse)
            return new List<(byte[], string)>();

        var itemFolder = Path.Combine(backupFolderPath, vm.Item.RelativePath);

        if (!Directory.Exists(itemFolder))
        {
            _logger.LogWarning("Item folder not found: {Folder}", itemFolder);
            return new List<(byte[], string)>();
        }

        var parts = new List<(byte[], string)>();
        var files = Directory.GetFiles(itemFolder, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes    = await File.ReadAllBytesAsync(file, cancellationToken);
            var partPath = Path.GetRelativePath(itemFolder, file).Replace('\\', '/');
            parts.Add((bytes, partPath));
        }

        return parts;
    }

    private static FabricItemType ParseItemType(string type) =>
        type?.ToLowerInvariant() switch
        {
            "report"             => FabricItemType.Report,
            "semanticmodel"      => FabricItemType.SemanticModel,
            "notebook"           => FabricItemType.Notebook,
            "datapipeline"       => FabricItemType.DataPipeline,
            "dataflow"           => FabricItemType.Dataflow,
            "lakehouse"          => FabricItemType.Lakehouse,
            "warehouse"          => FabricItemType.Warehouse,
            "kqldatabase"        => FabricItemType.KQLDatabase,
            "eventhouse"         => FabricItemType.Eventhouse,
            "environment"        => FabricItemType.Environment,
            "sparkjobdefinition" => FabricItemType.SparkJobDefinition,
            _                    => FabricItemType.Unknown
        };

    private static string GetItemIcon(FabricItemType type) => type switch
    {
        FabricItemType.Report             => "📈",
        FabricItemType.SemanticModel      => "🔷",
        FabricItemType.Notebook           => "📓",
        FabricItemType.DataPipeline       => "🔄",
        FabricItemType.Dataflow           => "💧",
        FabricItemType.Lakehouse          => "🏠",
        FabricItemType.Warehouse          => "🏭",
        FabricItemType.KQLDatabase        => "🔍",
        FabricItemType.Eventhouse         => "⚡",
        FabricItemType.Environment        => "🌐",
        FabricItemType.SparkJobDefinition => "✨",
        _                                 => "📄"
    };
}
