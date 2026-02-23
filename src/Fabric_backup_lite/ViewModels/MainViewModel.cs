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
    //  Properties                                                          //
    // ------------------------------------------------------------------ //

    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    private string _userDisplayName = "No autenticado";
    public string UserDisplayName
    {
        get => _userDisplayName;
        set => SetProperty(ref _userDisplayName, value);
    }

    private string _selectedItemsInfo = "0 ítems seleccionados";
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

    private string _statusMessage = "Listo";
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
    //  Commands                                                            //
    // ------------------------------------------------------------------ //

    public ICommand SignInCommand { get; }
    public ICommand SelectDestinationCommand { get; }
    public ICommand StartBackupCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }

    // ------------------------------------------------------------------ //
    //  Constructor                                                         //
    // ------------------------------------------------------------------ //

    public MainViewModel(
        IAuthenticationService authService,
        IFabricApiClient fabricClient,
        IBackupService backupService,
        ILogger<MainViewModel> logger)
    {
        _authService   = authService;
        _fabricClient  = fabricClient;
        _backupService = backupService;
        _logger        = logger;

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
    }

    // ------------------------------------------------------------------ //
    //  Sign In                                                             //
    // ------------------------------------------------------------------ //

    private async Task SignInAsync()
    {
        try
        {
            AddLog("Iniciando sesión...");
            StatusMessage = "Iniciando sesión...";

            await _authService.SignInAsync();

            IsAuthenticated = true;
            UserDisplayName = _authService.UserDisplayName ?? "Usuario desconocido";

            AddLog($"Sesión iniciada como {UserDisplayName}");
            AddLog("Cargando workspaces...");

            _workspaces = await _fabricClient.GetWorkspacesAsync();

            WorkspaceTreeNodes.Clear();
            foreach (var workspace in _workspaces)
                WorkspaceTreeNodes.Add(WorkspaceTreeNode.CreateWorkspace(workspace));

            AddLog($"Se encontraron {_workspaces.Count} workspace(s)");
            StatusMessage = $"Autenticado — {_workspaces.Count} workspace(s) disponibles";

            ((RelayCommand)SelectAllCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeselectAllCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            AddLog($"Error: {ex.Message}");
            StatusMessage = "Error al iniciar sesión";
            MessageBox.Show($"No se pudo iniciar sesión: {ex.Message}", "Error",
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
                    Name     = "Sin elementos",
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
            AddLog($"Cargados {items.Count} ítem(s) en '{node.Workspace.Name}'");
            RefreshSelectionInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load items for workspace {Id}", node.Workspace.Id);
            node.Children.Clear();
            node.Children.Add(new WorkspaceTreeNode
            {
                Name     = $"Error: {ex.Message}",
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
            ? "0 ítems seleccionados"
            : ws == 1
                ? $"{count} ítem(s) seleccionado(s)"
                : $"{count} ítem(s) en {ws} workspace(s)";

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
            Title           = "Seleccioná la carpeta de destino",
            FileName        = "SelectFolder",
            Filter          = "Folder|*.folder",
            CheckFileExists = false,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            AddLog($"Destino: {DestinationPath}");
            StatusMessage = "Carpeta de destino seleccionada";
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
                "Seleccioná al menos un ítem en el árbol de workspaces para hacer backup.",
                "Sin ítems seleccionados",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        IsBackupInProgress = true;
        ProgressValue = 0;

        try
        {
            AddLog($"Iniciando backup de {checkedItems.Count} ítem(s)...");
            StatusMessage = "Backup en progreso...";

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
                AddLog("✓ Backup completado exitosamente!");
                AddLog($"  Ítems guardados: {result.ItemsBackedUp}");
                AddLog($"  Ubicación: {result.BackupPath}");
                StatusMessage = "Backup completado";

                MessageBox.Show(
                    $"Backup completado exitosamente!\n\nÍtems guardados: {result.ItemsBackedUp}\nUbicación: {result.BackupPath}",
                    "Backup completo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLog("⚠ Backup completado con errores:");
                AddLog($"  Ítems guardados: {result.ItemsBackedUp}");
                foreach (var error in result.Errors)
                    AddLog($"    - {error}");

                StatusMessage = $"Backup con {result.Errors.Count} error(es)";

                MessageBox.Show(
                    $"Backup completado con errores.\n\nÍtems guardados: {result.ItemsBackedUp}\nErrores: {result.Errors.Count}\n\nRevisá el log para más detalles.",
                    "Backup con errores",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("Backup cancelado por el usuario");
            StatusMessage = "Backup cancelado";
            MessageBox.Show("El backup fue cancelado.", "Cancelado",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            AddLog($"✗ Error en el backup: {ex.Message}");
            StatusMessage = "Error en el backup";
            MessageBox.Show($"Error en el backup: {ex.Message}", "Error",
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
        AddLog("Cancelando backup...");
        StatusMessage = "Cancelando...";
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
