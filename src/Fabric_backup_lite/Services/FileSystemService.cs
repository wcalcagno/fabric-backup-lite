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

        // Crear subdirectorios por tipo. Derivado del enum vía el catálogo central para que
        // nunca se desincronice cuando se agregan tipos nuevos.
        var subDirs = Enum.GetValues<FabricItemType>()
            .Where(t => t != FabricItemType.Unknown)
            .Select(FabricItemCatalog.SubFolder)
            .Distinct();

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

    /// <summary>
    /// Writes workspace-level metadata sidecar files (settings, role assignments, connections)
    /// into a "_workspace" folder under the backup root. Each entry is { fileName => rawJson }.
    /// </summary>
    public async Task SaveWorkspaceMetadataAsync(
        string backupPath,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        if (metadata.Count == 0)
            return;

        var folder = Path.Combine(backupPath, "_workspace");
        Directory.CreateDirectory(folder);

        foreach (var (fileName, json) in metadata)
        {
            var path = Path.Combine(folder, fileName);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
            _logger.LogInformation("Saved workspace metadata: {Path}", path);
        }
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

    public string GetSubFolderForType(FabricItemType type) => FabricItemCatalog.SubFolder(type);

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
