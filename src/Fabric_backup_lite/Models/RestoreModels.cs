using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fabric_backup_lite.Models;

/// <summary>Summarises a discovered backup folder; shown in the "backup version" ComboBox.</summary>
public class BackupSummary
{
    public string BackupFolderPath { get; set; } = string.Empty;
    public BackupMetadata Metadata { get; set; } = new();

    /// <summary>"WorkspaceName — 2025-01-15 14:30 (12 items)"</summary>
    public string DisplayLabel { get; set; } = string.Empty;
}

/// <summary>One item from manifest.json rendered as a checkable row in the restore ListView.</summary>
public class RestoreItemViewModel : INotifyPropertyChanged
{
    public BackupItemInfo Item { get; init; } = new();
    public FabricItemType ItemType { get; init; }

    /// <summary>False for Warehouse — cannot be restored via API (OneLake upload required).</summary>
    public bool IsRestorable { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public string TypeIcon { get; init; } = string.Empty;

    /// <summary>Tooltip shown on rows where IsRestorable = false.</summary>
    public string TooltipText { get; init; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Progress snapshot reported during a restore operation.</summary>
public class RestoreProgress
{
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public string CurrentItem { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result returned after a restore operation completes.</summary>
public class RestoreResult
{
    public bool Success { get; set; }
    public int ItemsRestored { get; set; }
    public List<string> Errors { get; set; } = new();
    public string DestinationWorkspaceId { get; set; } = string.Empty;
    public string DestinationWorkspaceName { get; set; } = string.Empty;
}
