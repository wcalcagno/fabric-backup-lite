using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Fabric_backup_lite.Services;

namespace Fabric_backup_lite.Models;

public enum WorkspaceNodeType
{
    Workspace,
    FabricCategory,
    PowerBICategory,
    ItemTypeGroup,
    FabricItem,
    PowerBIItem,
    Placeholder
}

public class WorkspaceTreeNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isExpanded;
    private bool _isLoading;
    private bool? _isChecked = false;

    public WorkspaceTreeNode()
    {
        // Subscribe to language changes via WPF's weak-event manager to avoid memory leaks.
        // When the language switches, refresh the tooltip and (if applicable) the loading placeholder name.
        PropertyChangedEventManager.AddHandler(
            LocalizationService.Instance,
            OnLocalizationChanged,
            string.Empty);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TooltipText));
        if (IsLoadingPlaceholder)
            Name = LocalizationService.Instance.LoadingPlaceholder;
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string TypeIcon { get; set; } = string.Empty;
    public WorkspaceNodeType NodeType { get; set; }
    public WorkspaceTreeNode? Parent { get; set; }
    public Workspace? Workspace { get; set; }
    public FabricItem? FabricItem { get; set; }
    public bool IsLoaded { get; set; }
    public bool IsLoadingPlaceholder { get; set; }

    /// <summary>True for types Microsoft does not yet expose a getDefinition API for.</summary>
    public bool IsUnsupported { get; set; }

    /// <summary>True for types that can be exported (checkbox enabled).</summary>
    public bool IsSupported => !IsUnsupported;

    /// <summary>Shown as a tooltip on unsupported nodes; null for supported nodes.</summary>
    public string? TooltipText => IsUnsupported
        ? LocalizationService.Instance.UnsupportedTooltip
        : null;

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, updateChildren: true, updateParent: true);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public ObservableCollection<WorkspaceTreeNode> Children { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    internal void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (_isChecked == value) return;
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        if (updateChildren && _isChecked.HasValue)
            foreach (var child in Children)
                child.SetIsChecked(_isChecked, updateChildren: true, updateParent: false);

        if (updateParent)
            Parent?.VerifyCheckedState();
    }

    public void VerifyCheckedState()
    {
        // Exclude placeholders and unsupported items — they are always unchecked and
        // should not affect the tri-state of ancestor nodes.
        var relevant = Children.Where(c => c.NodeType != WorkspaceNodeType.Placeholder && !c.IsUnsupported).ToList();
        if (relevant.Count == 0) return;

        bool allChecked   = relevant.All(c => c.IsChecked == true);
        bool allUnchecked = relevant.All(c => c.IsChecked == false);
        bool? newState    = allChecked ? true : allUnchecked ? false : null;

        SetIsChecked(newState, updateChildren: false, updateParent: true);
    }

    // --- Factory methods ---

    public static WorkspaceTreeNode CreateWorkspace(Workspace workspace)
        => new()
        {
            Name      = workspace.Name,
            TypeIcon  = "📁",
            NodeType  = WorkspaceNodeType.Workspace,
            Workspace = workspace,
            IsLoaded  = false,
            IsChecked = false,
            Children  = new ObservableCollection<WorkspaceTreeNode>
            {
                new()
                {
                    Name                = LocalizationService.Instance.LoadingPlaceholder,
                    TypeIcon            = "⏳",
                    NodeType            = WorkspaceNodeType.Placeholder,
                    IsLoadingPlaceholder = true
                }
            }
        };

    public static WorkspaceTreeNode CreatePlaceholder(string text)
        => new()
        {
            Name     = text,
            TypeIcon = "⏳",
            NodeType = WorkspaceNodeType.Placeholder
        };

    public static WorkspaceTreeNode CreateFabricCategory(WorkspaceTreeNode parent, int count)
        => new()
        {
            Name      = $"Fabric Items ({count})",
            TypeIcon  = "⚙️",
            NodeType  = WorkspaceNodeType.FabricCategory,
            Parent    = parent,
            IsExpanded = true,
            IsLoaded  = true,
            IsChecked = false
        };

    public static WorkspaceTreeNode CreatePowerBICategory(WorkspaceTreeNode parent, int count)
        => new()
        {
            Name      = $"Power BI ({count})",
            TypeIcon  = "📊",
            NodeType  = WorkspaceNodeType.PowerBICategory,
            Parent    = parent,
            IsExpanded = true,
            IsLoaded  = true,
            IsChecked = false
        };

    private static bool ItemTypeIsUnsupported(FabricItemType type) =>
        type is FabricItemType.Lakehouse or FabricItemType.Warehouse or FabricItemType.KQLDatabase or FabricItemType.Unknown;

    public static WorkspaceTreeNode CreateItemTypeGroup(FabricItemType type, WorkspaceTreeNode parent, int count)
        => new()
        {
            Name          = $"{GetTypePluralName(type)} ({count})",
            TypeIcon      = GetItemIcon(type),
            NodeType      = WorkspaceNodeType.ItemTypeGroup,
            Parent        = parent,
            IsExpanded    = true,
            IsLoaded      = true,
            IsChecked     = false,
            IsUnsupported = ItemTypeIsUnsupported(type)
        };

    private static string GetTypePluralName(FabricItemType type) => type switch
    {
        FabricItemType.Report        => "Reports",
        FabricItemType.SemanticModel => "Semantic Models",
        FabricItemType.Notebook      => "Notebooks",
        FabricItemType.DataPipeline  => "Data Pipelines",
        FabricItemType.Dataflow      => "Dataflows",
        FabricItemType.Lakehouse     => "Lakehouses",
        FabricItemType.Warehouse     => "Warehouses",
        FabricItemType.KQLDatabase   => "KQL Databases",
        _                            => "Other"
    };

    public static WorkspaceTreeNode CreateItem(FabricItem item, WorkspaceTreeNode parent)
    {
        bool isPowerBI = item.Type is FabricItemType.Report or FabricItemType.SemanticModel;
        return new()
        {
            Name          = item.DisplayName,
            TypeIcon      = GetItemIcon(item.Type),
            NodeType      = isPowerBI ? WorkspaceNodeType.PowerBIItem : WorkspaceNodeType.FabricItem,
            Parent        = parent,
            FabricItem    = item,
            IsLoaded      = true,
            IsChecked     = false,
            IsUnsupported = ItemTypeIsUnsupported(item.Type)
        };
    }

    private static string GetItemIcon(FabricItemType type) => type switch
    {
        FabricItemType.Report        => "📈",
        FabricItemType.SemanticModel => "🔷",
        FabricItemType.Notebook      => "📓",
        FabricItemType.DataPipeline  => "🔄",
        FabricItemType.Dataflow      => "💧",
        FabricItemType.Lakehouse     => "🏠",
        FabricItemType.Warehouse     => "🏭",
        FabricItemType.KQLDatabase   => "🔍",
        _                            => "📄"
    };
}
