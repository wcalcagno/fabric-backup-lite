namespace Fabric_backup_lite.Models;

public class BackupMetadata
{
    public string Version { get; set; } = "2.0";
    public DateTime Timestamp { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public List<BackupItemInfo> Items { get; set; } = new();
}

public class BackupItemInfo
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public class BackupProgress
{
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public string CurrentItem { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class BackupResult
{
    public bool Success { get; set; }
    public int ItemsBackedUp { get; set; }
    public List<string> Errors { get; set; } = new();
    public string BackupPath { get; set; } = string.Empty;
}
