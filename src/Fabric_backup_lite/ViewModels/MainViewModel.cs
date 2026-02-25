using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows;
using Fabric_backup_lite.Models;
using Fabric_backup_lite.Services;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authService;
    private readonly IFabricApiClient _fabricClient;
    private readonly IBackupService _backupService;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    // ------------------------------------------------------------------ //
    //  Localization — expose singleton so XAML can bind to L.SomeProp    //
    // ------------------------------------------------------------------ //

    public LocalizationService L => LocalizationService.Instance;

    // ------------------------------------------------------------------ //
    //  Properties                                                          //
    // ------------------------------------------------------------------ //

    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    private string _userDisplayName = LocalizationService.Instance.NotAuthenticated;
    public string UserDisplayName
    {
        get => _userDisplayName;
        set => SetProperty(ref _userDisplayName, value);
    }

    private string _selectedItemsInfo = LocalizationService.Instance.ZeroItemsSelected;
    public string SelectedItemsInfo
    {
        get => _selectedItemsInfo;
        set => SetProperty(ref _selectedItemsInfo, value);
    }

    private string _destinationPath = string.Empty;
    public string DestinationPath
    {
        get => _destinationPath;
        set
        {
            if (SetProperty(ref _destinationPath, value))
                ((AsyncRelayCommand)StartBackupCommand).RaiseCanExecuteChanged();
        }
    }

    private ObservableCollection<string> _logMessages = new();
    public ObservableCollection<string> LogMessages
    {
        get => _logMessages;
        set => SetProperty(ref _logMessages, value);
    }

    private bool _isBackupInProgress;
    public bool IsBackupInProgress
    {
        get => _isBackupInProgress;
        set
        {
            if (SetProperty(ref _isBackupInProgress, value))
            {
                ((AsyncRelayCommand)SignInCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SelectDestinationCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)StartBackupCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SelectAllCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeselectAllCommand).RaiseCanExecuteChanged();
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

    private string _statusMessage = LocalizationService.Instance.Ready;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ------------------------------------------------------------------ //
    //  Workspace Tree                                                      //
    // ------------------------------------------------------------------ //

    private ObservableCollection<WorkspaceTreeNode> _workspaceTreeNodes = new();
    public ObservableCollection<WorkspaceTreeNode> WorkspaceTreeNodes
    {
        get => _workspaceTreeNodes;
        set => SetProperty(ref _workspaceTreeNodes, value);
    }

    // Internal workspace list for name lookups
    private List<Workspace> _workspaces = new();

    // ------------------------------------------------------------------ //
    //  Restore ViewModel (exposed so the Restore tab can bind to it)    //
    // ------------------------------------------------------------------ //

    public RestoreViewModel RestoreVm { get; }

    // ------------------------------------------------------------------ //
    //  Commands                                                            //
    // ------------------------------------------------------------------ //

    public ICommand SignInCommand { get; }
    public ICommand SelectDestinationCommand { get; }
    public ICommand StartBackupCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SetLanguageEsCommand { get; }
    public ICommand SetLanguageEnCommand { get; }

    // ------------------------------------------------------------------ //
    //  Constructor                                                         //
    // ------------------------------------------------------------------ //

    public MainViewModel(
        IAuthenticationService authService,
        IFabricApiClient fabricClient,
        IBackupService backupService,
        RestoreViewModel restoreViewModel,
        ILogger<MainViewModel> logger)
    {
        _authService   = authService;
        _fabricClient  = fabricClient;
        _backupService = backupService;
        _logger        = logger;
        RestoreVm      = restoreViewModel;

        SignInCommand = new AsyncRelayCommand(
            async _ => await SignInAsync(),
            _ => !IsBackupInProgress);

        SelectDestinationCommand = new RelayCommand(
            _ => SelectDestination(),
            _ => !IsBackupInProgress);

        StartBackupCommand = new AsyncRelayCommand(
            async _ => await StartBackupAsync(),
            _ => CanStartBackup());

        CancelCommand = new RelayCommand(
            _ => CancelBackup(),
            _ => IsBackupInProgress);

        SelectAllCommand = new RelayCommand(
            _ => SetAllChecked(true),
            _ => !IsBackupInProgress && IsAuthenticated);

        DeselectAllCommand = new RelayCommand(
            _ => SetAllChecked(false),
            _ => !IsBackupInProgress && IsAuthenticated);

        SetLanguageEsCommand = new RelayCommand(_ => L.Language = "es");
        SetLanguageEnCommand = new RelayCommand(_ => L.Language = "en");

        // Refresh language-dependent properties when language changes
        L.PropertyChanged += (_, _) =>
        {
            if (!IsAuthenticated)
                UserDisplayName = L.NotAuthenticated;
            if (!IsBackupInProgress)
                StatusMessage = L.Ready;
            RefreshSelectionInfo();
        };
    }

    // ------------------------------------------------------------------ //
    //  Sign In                                                             //
    // ------------------------------------------------------------------ //

    private async Task SignInAsync()
    {
        try
        {
            AddLog(L.SigningIn);
            StatusMessage = L.SigningIn;

            await _authService.SignInAsync();

            IsAuthenticated = true;
            UserDisplayName = _authService.UserDisplayName ?? L.UnknownUser;

            AddLog(string.Format(L.SessionStartedAsFmt, UserDisplayName));
            AddLog(L.LoadingWorkspaces);

            _workspaces = await _fabricClient.GetWorkspacesAsync();

            WorkspaceTreeNodes.Clear();
            foreach (var workspace in _workspaces)
                WorkspaceTreeNodes.Add(WorkspaceTreeNode.CreateWorkspace(workspace));

            AddLog(string.Format(L.FoundWorkspacesFmt, _workspaces.Count));
            StatusMessage = string.Format(L.AuthenticatedFmt, _workspaces.Count);

            ((RelayCommand)SelectAllCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeselectAllCommand).RaiseCanExecuteChanged();

            // Load workspaces for the Restore tab as well
            await RestoreVm.LoadWorkspacesForRestoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            AddLog(string.Format(L.ErrorFmt, ex.Message));
            StatusMessage = L.SignInErrorStatus;
            MessageBox.Show(string.Format(L.CouldNotSignInFmt, ex.Message), L.BackupErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------------ //
    //  Lazy-load workspace items into the tree                             //
    // ------------------------------------------------------------------ //

    public async Task LoadWorkspaceItemsAsync(WorkspaceTreeNode node)
    {
        if (node.IsLoaded || node.NodeType != WorkspaceNodeType.Workspace || node.Workspace == null)
            return;

        node.IsLoading = true;

        try
        {
            var items = await _fabricClient.GetWorkspaceItemsAsync(node.Workspace.Id);

            node.Children.Clear();

            var fabricItems  = items.Where(i => i.Type is not FabricItemType.Report and not FabricItemType.SemanticModel).ToList();
            var powerBIItems = items.Where(i => i.Type is FabricItemType.Report or FabricItemType.SemanticModel).ToList();

            if (fabricItems.Count > 0)
            {
                var cat = WorkspaceTreeNode.CreateFabricCategory(node, fabricItems.Count);
                foreach (var group in fabricItems.GroupBy(i => i.Type).OrderBy(g => g.Key.ToString()))
                {
                    var typeGroup = WorkspaceTreeNode.CreateItemTypeGroup(group.Key, cat, group.Count());
                    foreach (var item in group)
                        typeGroup.Children.Add(WorkspaceTreeNode.CreateItem(item, typeGroup));
                    cat.Children.Add(typeGroup);
                }
                node.Children.Add(cat);
            }

            if (powerBIItems.Count > 0)
            {
                var cat = WorkspaceTreeNode.CreatePowerBICategory(node, powerBIItems.Count);
                foreach (var group in powerBIItems.GroupBy(i => i.Type).OrderBy(g => g.Key.ToString()))
                {
                    var typeGroup = WorkspaceTreeNode.CreateItemTypeGroup(group.Key, cat, group.Count());
                    foreach (var item in group)
                        typeGroup.Children.Add(WorkspaceTreeNode.CreateItem(item, typeGroup));
                    cat.Children.Add(typeGroup);
                }
                node.Children.Add(cat);
            }

            if (fabricItems.Count == 0 && powerBIItems.Count == 0)
            {
                node.Children.Add(new WorkspaceTreeNode
                {
                    Name     = L.NoItems,
                    TypeIcon = "ℹ️",
                    NodeType = WorkspaceNodeType.Placeholder
                });
            }

            // Si el workspace estaba marcado, propagar a los nuevos hijos
            if (node.IsChecked.HasValue)
                node.SetIsChecked(node.IsChecked, updateChildren: true, updateParent: false);

            // Actualizar el nombre del workspace con el total de ítems
            int totalItems = fabricItems.Count + powerBIItems.Count;
            node.Name = $"{node.Workspace.Name} ({totalItems})";

            node.IsLoaded = true;
            AddLog(string.Format(L.LoadedItemsFmt, items.Count, node.Workspace.Name));
            RefreshSelectionInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load items for workspace {Id}", node.Workspace.Id);
            node.Children.Clear();
            node.Children.Add(new WorkspaceTreeNode
            {
                Name     = string.Format(L.ErrorFmt, ex.Message),
                TypeIcon = "❌",
                NodeType = WorkspaceNodeType.Placeholder
            });
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    // ------------------------------------------------------------------ //
    //  Selection helpers                                                   //
    // ------------------------------------------------------------------ //

    public void RefreshSelectionInfo()
    {
        var checked_ = GetCheckedItems();
        int count    = checked_.Count;
        int ws       = checked_.Select(i => i.workspaceId).Distinct().Count();

        SelectedItemsInfo = count == 0
            ? L.ZeroItemsSelected
            : ws == 1
                ? string.Format(L.ItemsSelectedSingleFmt, count)
                : string.Format(L.ItemsSelectedMultiFmt, count, ws);

        ((AsyncRelayCommand)StartBackupCommand).RaiseCanExecuteChanged();
    }

    private void SetAllChecked(bool value)
    {
        foreach (var wsNode in WorkspaceTreeNodes)
            wsNode.SetIsChecked(value, updateChildren: true, updateParent: false);

        RefreshSelectionInfo();
    }

    private List<(string workspaceId, string workspaceName, FabricItem item)> GetCheckedItems()
    {
        var result = new List<(string, string, FabricItem)>();

        foreach (var wsNode in WorkspaceTreeNodes)
        {
            if (wsNode.Workspace == null) continue;

            foreach (var catNode in wsNode.Children)
            {
                if (catNode.NodeType is not (WorkspaceNodeType.FabricCategory or WorkspaceNodeType.PowerBICategory))
                    continue;

                foreach (var typeGroupNode in catNode.Children)
                {
                    if (typeGroupNode.NodeType != WorkspaceNodeType.ItemTypeGroup)
                        continue;

                    foreach (var itemNode in typeGroupNode.Children)
                    {
                        if (itemNode.IsChecked == true && itemNode.FabricItem != null)
                            result.Add((wsNode.Workspace.Id, wsNode.Workspace.Name, itemNode.FabricItem));
                    }
                }
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ //
    //  Destination picker                                                  //
    // ------------------------------------------------------------------ //

    private void SelectDestination()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title           = L.SelectDestinationTitle,
            FileName        = "SelectFolder",
            Filter          = "Folder|*.folder",
            CheckFileExists = false,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            AddLog(string.Format(L.DestinationFmt, DestinationPath));
            StatusMessage = L.DestinationFolderSelectedSt;
        }
    }

    // ------------------------------------------------------------------ //
    //  Backup                                                              //
    // ------------------------------------------------------------------ //

    private async Task StartBackupAsync()
    {
        var checkedItems = GetCheckedItems();

        if (checkedItems.Count == 0)
        {
            MessageBox.Show(
                L.SelectAtLeastOneMsg,
                L.NoItemsSelectedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        IsBackupInProgress = true;
        ProgressValue = 0;

        try
        {
            AddLog(string.Format(L.StartingBackupFmt, checkedItems.Count));
            StatusMessage = L.BackupInProgress;

            var progress = new Progress<BackupProgress>(p =>
            {
                ProgressMaximum = p.TotalItems > 0 ? p.TotalItems : 100;
                ProgressValue   = p.CompletedItems;
                StatusMessage   = p.Message;
                AddLog(p.Message);
            });

            var result = await _backupService.BackupSelectedItemsAsync(
                checkedItems,
                DestinationPath,
                progress,
                _cancellationTokenSource.Token);

            if (result.Success)
            {
                AddLog(L.BackupCompletedLog);
                AddLog(string.Format(L.ItemsSavedFmt, result.ItemsBackedUp));
                AddLog(string.Format(L.LocationFmt, result.BackupPath));
                StatusMessage = L.BackupCompletedStatus;

                MessageBox.Show(
                    string.Format(L.BackupCompletedMsgFmt, result.ItemsBackedUp, result.BackupPath),
                    L.BackupCompleteTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLog(L.BackupWithErrorsLog);
                AddLog(string.Format(L.ItemsSavedFmt, result.ItemsBackedUp));
                foreach (var error in result.Errors)
                    AddLog($"    - {error}");

                StatusMessage = string.Format(L.BackupWithErrorsStatusFmt, result.Errors.Count);

                MessageBox.Show(
                    string.Format(L.BackupWithErrorsMsgFmt, result.ItemsBackedUp, result.Errors.Count),
                    L.BackupWithErrorsTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            AddLog(L.BackupCancelledLog);
            StatusMessage = L.BackupCancelledStatus;
            MessageBox.Show(L.BackupCancelledMsg, L.BackupCancelledTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            AddLog(string.Format(L.BackupErrorFmt, ex.Message));
            StatusMessage = L.BackupErrorStatus;
            MessageBox.Show(string.Format(L.BackupErrorMsgFmt, ex.Message), L.BackupErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBackupInProgress = false;
            ProgressValue = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void CancelBackup()
    {
        _cancellationTokenSource?.Cancel();
        AddLog(L.CancellingBackupLog);
        StatusMessage = L.CancellingStatus;
    }

    private bool CanStartBackup()
    {
        return !IsBackupInProgress && !string.IsNullOrWhiteSpace(DestinationPath);
    }

    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogMessages.Add($"[{timestamp}] {message}");

        while (LogMessages.Count > 500)
            LogMessages.RemoveAt(0);
    }
}
