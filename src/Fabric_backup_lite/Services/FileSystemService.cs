using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fabric_backup_lite.Models;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.Services;

public class FileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public string CreateBackupDirectory(
        string baseDestination,
        string tenantId,
        string workspaceId,
        string workspaceName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sanitizedWorkspaceName = SanitizeFileName(workspaceName);

        var backupPath = Path.Combine(
            baseDestination,
            tenantId,
            $"{sanitizedWorkspaceName}_{workspaceId}",
            $"{timestamp}_backup"
        );

        Directory.CreateDirectory(backupPath);
        _logger.LogInformation("Created backup directory: {Path}", backupPath);

        // Crear subdirectorios por tipo
        var subDirs = new[] {
            "Reports",
            "SemanticModels",
            "Notebooks",
            "Pipelines",
            "Dataflows",
            "Lakehouses",
            "Warehouses",
            "KQLDatabases",
            "Eventhouses",
            "Environments",
            "SparkJobDefinitions"
        };

        foreach (var subDir in subDirs)
        {
            Directory.CreateDirectory(Path.Combine(backupPath, subDir));
        }

        return backupPath;
    }

    public async Task<string> SaveItemDefinitionAsync(
        string backupPath,
        FabricItem item,
        List<(byte[] content, string partPath)> parts,
        CancellationToken cancellationToken = default)
    {
        var subFolder     = GetSubFolderForType(item.Type);
        var sanitizedName = SanitizeFileName(item.DisplayName);
        var timestamp     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Each item gets its own subfolder so multi-part items (PBIR reports) don't collide.
        var itemFolder    = $"{sanitizedName}_{timestamp}";
        var itemPath      = Path.Combine(backupPath, subFolder, itemFolder);

        Directory.CreateDirectory(itemPath);

        foreach (var (content, partPath) in parts)
        {
            // partPath may contain subdirectories (e.g. "pages/page1.json" in PBIR)
            var fullPath = Path.Combine(itemPath, partPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await WaitForFileUnlockedAsync(fullPath, cancellationToken);
            await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

            _logger.LogInformation("Saved {ItemType} '{ItemName}' → {Path}",
                item.Type, item.DisplayName, fullPath);
        }

        return Path.Combine(subFolder, itemFolder);
    }

    public async Task SaveManifestAsync(
        string backupPath,
        BackupMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(backupPath, "manifest.json");

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(manifestPath, json, Encoding.UTF8, cancellationToken);

        _logger.LogInformation("Manifest saved: {Path}", manifestPath);
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "unnamed";
        }

        // Normalizar y remover caracteres no ASCII
        var normalized = fileName.Normalize(NormalizationForm.FormD);
        var regex = new Regex(@"[^a-zA-Z0-9\s\-_]");
        var sanitized = regex.Replace(normalized, "");

        // Reemplazar espacios múltiples con uno solo
        sanitized = Regex.Replace(sanitized, @"\s+", " ");

        // Trim y limitar longitud
        sanitized = sanitized.Trim();
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    public string GetSubFolderForType(FabricItemType type)
    {
        return type switch
        {
            FabricItemType.Report            => "Reports",
            FabricItemType.SemanticModel     => "SemanticModels",
            FabricItemType.Notebook          => "Notebooks",
            FabricItemType.DataPipeline      => "Pipelines",
            FabricItemType.Dataflow          => "Dataflows",
            FabricItemType.Lakehouse         => "Lakehouses",
            FabricItemType.Warehouse         => "Warehouses",
            FabricItemType.KQLDatabase       => "KQLDatabases",
            FabricItemType.Eventhouse        => "Eventhouses",
            FabricItemType.Environment       => "Environments",
            FabricItemType.SparkJobDefinition => "SparkJobDefinitions",
            _                                => "Other"
        };
    }

    private bool IsFileLocked(FileInfo file)
    {
        if (!file.Exists)
        {
            return false;
        }

        try
        {
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            stream.Close();
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private async Task WaitForFileUnlockedAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var fileInfo = new FileInfo(filePath);
        var attempts = 0;
        const int maxAttempts = 10;

        while (IsFileLocked(fileInfo) && attempts < maxAttempts)
        {
            _logger.LogWarning("File {Path} is locked, waiting... (attempt {Attempt}/{Max})",
                filePath, attempts + 1, maxAttempts);

            await Task.Delay(500, cancellationToken);
            attempts++;
        }

        if (attempts >= maxAttempts)
        {
            throw new IOException($"File {filePath} remains locked after {maxAttempts} attempts");
        }
    }
}
