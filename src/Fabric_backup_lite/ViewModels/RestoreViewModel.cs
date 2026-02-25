using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Fabric_backup_lite.Models;
using Fabric_backup_lite.Services;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.ViewModels;

public class RestoreViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authService;
    private readonly IFabricApiClient       _fabricClient;
    private readonly IRestoreService        _restoreService;
    private readonly ILogger<RestoreViewModel> _logger;
    private CancellationTokenSource? _cts;

    // Sentinel workspace that represents "＋ New workspace" in the ComboBox
    private static readonly Workspace NewWorkspaceSentinel = new()
    {
        Id   = "__new__",
        Name = LocalizationService.Instance.NewWorkspaceOption
    };

    // ------------------------------------------------------------------ //
    //  Localization                                                        //
    // ------------------------------------------------------------------ //

    public LocalizationService L => LocalizationService.Instance;

    // ------------------------------------------------------------------ //
    //  Properties — Source (backup root + discovered backups)             //
    // ------------------------------------------------------------------ //

    private string _sourceRootFolder = string.Empty;
    public string SourceRootFolder
    {
        get => _sourceRootFolder;
        set => SetProperty(ref _sourceRootFolder, value);
    }

    private ObservableCollection<BackupSummary> _discoveredBackups = new();
    public ObservableCollection<BackupSummary> DiscoveredBackups
    {
        get => _discoveredBackups;
        set => SetProperty(ref _discoveredBackups, value);
    }

    private BackupSummary? _selectedBackup;
    public BackupSummary? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value) && value != null)
                _ = LoadManifestItemsAsync(value.BackupFolderPath);
        }
    }

    // ------------------------------------------------------------------ //
    //  Properties — Item list (from manifest)                             //
    // ------------------------------------------------------------------ //

    private ObservableCollection<RestoreItemViewModel> _manifestItems = new();
    public ObservableCollection<RestoreItemViewModel> ManifestItems
    {
        get => _manifestItems;
        set { SetProperty(ref _manifestItems, value); RefreshSelectionInfo(); }
    }

    private string _selectedItemsInfo = string.Empty;
    public string SelectedItemsInfo
    {
        get => _selectedItemsInfo;
        set => SetProperty(ref _selectedItemsInfo, value);
    }

    // ------------------------------------------------------------------ //
    //  Properties — Destination workspace                                 //
    // ------------------------------------------------------------------ //

    private ObservableCollection<Workspace> _availableWorkspaces = new();
    public ObservableCollection<Workspace> AvailableWorkspaces
    {
        get => _availableWorkspaces;
        set => SetProperty(ref _availableWorkspaces, value);
    }

    private Workspace? _selectedDestinationWorkspace;
    public Workspace? SelectedDestinationWorkspace
    {
        get => _selectedDestinationWorkspace;
        set
        {
            if (SetProperty(ref _selectedDestinationWorkspace, value))
            {
                OnPropertyChanged(nameof(IsNewWorkspace));
                ((AsyncRelayCommand)StartRestoreCommand).RaiseCanExecuteChanged();

                if (IsNewWorkspace && AvailableCapacities.Count == 0)
                    _ = LoadCapacitiesAsync();
            }
        }
    }

    public bool IsNewWorkspace => _selectedDestinationWorkspace?.Id == "__new__";

    private string _newWorkspaceName = string.Empty;
    public string NewWorkspaceName
    {
        get => _newWorkspaceName;
        set => SetProperty(ref _newWorkspaceName, value);
    }

    private ObservableCollection<FabricCapacity> _availableCapacities = new();
    public ObservableCollection<FabricCapacity> AvailableCapacities
    {
        get => _availableCapacities;
        set => SetProperty(ref _availableCapacities, value);
    }

    private FabricCapacity? _selectedCapacity;
    public FabricCapacity? SelectedCapacity
    {
        get => _selectedCapacity;
        set => SetProperty(ref _selectedCapacity, value);
    }

    // ------------------------------------------------------------------ //
    //  Properties — Progress & status                                      //
    // ------------------------------------------------------------------ //

    private bool _isRestoreInProgress;
    public bool IsRestoreInProgress
    {
        get => _isRestoreInProgress;
        set
        {
            if (SetProperty(ref _isRestoreInProgress, value))
            {
                ((AsyncRelayCommand)StartRestoreCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelRestoreCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SelectAllItemsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeselectAllItemsCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private int _progressMaximum = 100;
    public int ProgressMaximum
    {
        get => _progressMaximum;
        set => SetProperty(ref _progressMaximum, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private ObservableCollection<string> _logMessages = new();
    public ObservableCollection<string> LogMessages
    {
        get => _logMessages;
        set => SetProperty(ref _logMessages, value);
    }

    // ------------------------------------------------------------------ //
    //  Commands                                                            //
    // ------------------------------------------------------------------ //

    public ICommand BrowseSourceFolderCommand  { get; }
    public ICommand SelectAllItemsCommand      { get; }
    public ICommand DeselectAllItemsCommand    { get; }
    public ICommand LoadWorkspacesCommand      { get; }
    public ICommand StartRestoreCommand        { get; }
    public ICommand CancelRestoreCommand       { get; }

    // ------------------------------------------------------------------ //
    //  Constructor                                                         //
    // ------------------------------------------------------------------ //

    public RestoreViewModel(
        IAuthenticationService    authService,
        IFabricApiClient          fabricClient,
        IRestoreService           restoreService,
        ILogger<RestoreViewModel> logger)
    {
        _authService    = authService;
        _fabricClient   = fabricClient;
        _restoreService = restoreService;
        _logger         = logger;

        StatusMessage = L.RestoreReady;

        BrowseSourceFolderCommand = new RelayCommand(
            _ => BrowseSourceFolder(),
            _ => !IsRestoreInProgress);

        SelectAllItemsCommand = new RelayCommand(
            _ => SetAllItemsChecked(true),
            _ => !IsRestoreInProgress && ManifestItems.Count > 0);

        DeselectAllItemsCommand = new RelayCommand(
            _ => SetAllItemsChecked(false),
            _ => !IsRestoreInProgress && ManifestItems.Count > 0);

        LoadWorkspacesCommand = new AsyncRelayCommand(
            async _ => await LoadWorkspacesForRestoreAsync(),
            _ => !IsRestoreInProgress);

        StartRestoreCommand = new AsyncRelayCommand(
            async _ => await StartRestoreAsync(),
            _ => CanStartRestore());

        CancelRestoreCommand = new RelayCommand(
            _ => CancelRestore(),
            _ => IsRestoreInProgress);

        // Refresh language-dependent properties on language switch
        L.PropertyChanged += (_, _) =>
        {
            StatusMessage = IsRestoreInProgress ? L.RestoreInProgress : L.RestoreReady;
            RefreshSelectionInfo();
            // Update sentinel name in the list
            if (NewWorkspaceSentinel != null)
                NewWorkspaceSentinel.Name = L.NewWorkspaceOption;
        };
    }

    // ------------------------------------------------------------------ //
    //  Browse source folder                                                //
    // ------------------------------------------------------------------ //

    private void BrowseSourceFolder()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title           = L.SelectSourceTitle,
            FileName        = "SelectFolder",
            Filter          = "Folder|*.folder",
            CheckFileExists = false,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() != true) return;

        SourceRootFolder = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        if (!string.IsNullOrEmpty(SourceRootFolder))
            _ = DiscoverBackupsAsync(SourceRootFolder);
    }

    private async Task DiscoverBackupsAsync(string rootFolder)
    {
        AddLog($"📂 {L.SourceFolderLabel}: {rootFolder}");
        StatusMessage = L.LoadingWorkspaces;

        try
        {
            var backups = await _restoreService.DiscoverBackupsAsync(rootFolder);
            DiscoveredBackups.Clear();
            foreach (var b in backups)
                DiscoveredBackups.Add(b);

            AddLog(backups.Count == 0
                ? L.NoBackupsFound
                : string.Format("{0} backup(s) detectado(s)", backups.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering backups");
            AddLog(string.Format(L.RestoreErrorFmt, ex.Message));
        }
        finally
        {
            StatusMessage = L.RestoreReady;
        }
    }

    // ------------------------------------------------------------------ //
    //  Load manifest items                                                 //
    // ------------------------------------------------------------------ //

    private async Task LoadManifestItemsAsync(string backupFolderPath)
    {
        try
        {
            ManifestItems.Clear();
            RefreshSelectionInfo();

            var items = await _restoreService.LoadManifestItemsAsync(backupFolderPath);

            foreach (var item in items)
            {
                item.PropertyChanged += (_, _) => RefreshSelectionInfo();
                ManifestItems.Add(item);
            }

            RefreshSelectionInfo();
            ((RelayCommand)SelectAllItemsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeselectAllItemsCommand).RaiseCanExecuteChanged();

            AddLog(string.Format(L.LoadedItemsFmt, items.Count,
                SelectedBackup?.Metadata.WorkspaceName ?? "backup"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading manifest");
            AddLog(string.Format(L.RestoreErrorFmt, ex.Message));
        }
    }

    // ------------------------------------------------------------------ //
    //  Load workspaces for destination ComboBox                           //
    // ------------------------------------------------------------------ //

    public async Task LoadWorkspacesForRestoreAsync()
    {
        if (!_authService.IsAuthenticated)
        {
            AddLog(L.NotAuthenticated);
            return;
        }

        AddLog(L.LoadingWorkspacesForRestore);

        try
        {
            var workspaces = await _fabricClient.GetWorkspacesAsync();

            AvailableWorkspaces.Clear();
            // Add sentinel as first item
            NewWorkspaceSentinel.Name = L.NewWorkspaceOption;
            AvailableWorkspaces.Add(NewWorkspaceSentinel);

            foreach (var ws in workspaces)
                AvailableWorkspaces.Add(ws);

            AddLog(string.Format(L.FoundWorkspacesFmt, workspaces.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workspaces for restore");
            AddLog(string.Format(L.RestoreErrorFmt, ex.Message));
        }
    }

    // ------------------------------------------------------------------ //
    //  Load capacities (lazy, on demand)                                  //
    // ------------------------------------------------------------------ //

    private async Task LoadCapacitiesAsync()
    {
        AddLog(L.LoadingCapacities);

        try
        {
            var capacities = await _fabricClient.GetCapacitiesAsync();
            AvailableCapacities.Clear();

            // Sort: trial capacities first
            foreach (var cap in capacities.OrderByDescending(c => c.IsTrial).ThenBy(c => c.DisplayName))
                AvailableCapacities.Add(cap);

            // Pre-select trial capacity if available
            SelectedCapacity = AvailableCapacities.FirstOrDefault(c => c.IsTrial)
                ?? AvailableCapacities.FirstOrDefault();

            if (capacities.Count == 0)
                AddLog(L.NoCapacitiesFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading capacities");
            AddLog(string.Format(L.RestoreErrorFmt, ex.Message));
        }
    }

    // ------------------------------------------------------------------ //
    //  Restore                                                             //
    // ------------------------------------------------------------------ //

    private bool CanStartRestore() =>
        !IsRestoreInProgress &&
        SelectedBackup != null &&
        SelectedDestinationWorkspace != null &&
        (!IsNewWorkspace || !string.IsNullOrWhiteSpace(NewWorkspaceName)) &&
        (!IsNewWorkspace || SelectedCapacity != null);

    private async Task StartRestoreAsync()
    {
        var selectedItems = ManifestItems.Where(i => i.IsChecked && i.IsRestorable).ToList();

        if (selectedItems.Count == 0)
        {
            MessageBox.Show(L.SelectAtLeastOneRestoreMsg, L.NoItemsSelectedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedBackup == null || SelectedDestinationWorkspace == null)
        {
            MessageBox.Show(L.NoBackupSelectedMsg, L.NoItemsSelectedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts = new CancellationTokenSource();
        IsRestoreInProgress = true;
        ProgressValue       = 0;

        try
        {
            string targetWorkspaceId;
            string targetWorkspaceName;

            // Create workspace if needed
            if (IsNewWorkspace)
            {
                AddLog(string.Format(L.CreatingWorkspaceLog, NewWorkspaceName));
                var newWs = await _fabricClient.CreateWorkspaceAsync(
                    NewWorkspaceName, SelectedCapacity!.Id, _cts.Token);
                targetWorkspaceId   = newWs.Id;
                targetWorkspaceName = newWs.Name;
                AddLog(string.Format(L.WorkspaceCreatedLog, newWs.Name, newWs.Id));
            }
            else
            {
                targetWorkspaceId   = SelectedDestinationWorkspace.Id;
                targetWorkspaceName = SelectedDestinationWorkspace.Name;
            }

            AddLog(string.Format(L.StartingBackupFmt, selectedItems.Count));
            StatusMessage = L.RestoreInProgress;

            var progress = new Progress<RestoreProgress>(p =>
            {
                ProgressMaximum = p.TotalItems > 0 ? p.TotalItems : 100;
                ProgressValue   = p.CompletedItems;
                StatusMessage   = p.Message;
                AddLog(p.Message);
            });

            var result = await _restoreService.RestoreItemsAsync(
                SelectedBackup.BackupFolderPath,
                targetWorkspaceId,
                targetWorkspaceName,
                selectedItems,
                progress,
                _cts.Token);

            if (result.Success)
            {
                AddLog(L.RestoreCompletedLog);
                AddLog(string.Format(L.ItemsRestoredFmt, result.ItemsRestored));
                StatusMessage = L.RestoreCompletedStatus;

                MessageBox.Show(
                    string.Format(L.RestoreCompletedMsgFmt, result.ItemsRestored, targetWorkspaceName),
                    L.RestoreCompleteTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLog(L.RestoreWithErrorsLog);
                AddLog(string.Format(L.ItemsRestoredFmt, result.ItemsRestored));
                foreach (var err in result.Errors)
                    AddLog($"    - {err}");
                StatusMessage = string.Format(L.RestoreWithErrorsStatusFmt, result.Errors.Count);

                MessageBox.Show(
                    string.Format(L.RestoreWithErrorsMsgFmt, result.ItemsRestored, result.Errors.Count),
                    L.RestoreWithErrorsTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            AddLog(L.RestoreCancelledLog);
            StatusMessage = L.RestoreCancelledStatus;
            MessageBox.Show(L.RestoreCancelledMsg, L.RestoreCancelledTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            AddLog(string.Format(L.RestoreErrorFmt, ex.Message));
            StatusMessage = L.RestoreErrorStatus;
            MessageBox.Show(string.Format(L.RestoreErrorMsgFmt, ex.Message), L.RestoreErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsRestoreInProgress = false;
            ProgressValue       = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelRestore()
    {
        _cts?.Cancel();
        AddLog(L.CancellingBackupLog);
        StatusMessage = L.CancellingStatus;
    }

    // ------------------------------------------------------------------ //
    //  Selection helpers                                                   //
    // ------------------------------------------------------------------ //

    private void SetAllItemsChecked(bool value)
    {
        foreach (var item in ManifestItems.Where(i => i.IsRestorable))
            item.IsChecked = value;
        RefreshSelectionInfo();
    }

    public void RefreshSelectionInfo()
    {
        var count = ManifestItems.Count(i => i.IsChecked && i.IsRestorable);
        SelectedItemsInfo = count == 0
            ? L.RestoreZeroSelected
            : string.Format(L.RestoreItemsSelectedFmt, count);

        ((AsyncRelayCommand)StartRestoreCommand).RaiseCanExecuteChanged();
    }

    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //

    private void AddLog(string message)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogMessages.Add($"[{ts}] {message}");
        while (LogMessages.Count > 500)
            LogMessages.RemoveAt(0);
    }
}
