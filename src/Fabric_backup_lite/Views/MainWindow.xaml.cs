using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Fabric_backup_lite.Models;
using Fabric_backup_lite.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fabric_backup_lite.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _serviceProvider = serviceProvider;
    }

    // ------------------------------------------------------------------
    // Settings button
    // ------------------------------------------------------------------

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    // ------------------------------------------------------------------
    // TreeView: lazy-load workspace items on first expand
    // ------------------------------------------------------------------

    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem treeItem)
            return;

        if (treeItem.DataContext is not WorkspaceTreeNode node)
            return;

        if (node.NodeType != WorkspaceNodeType.Workspace || node.IsLoaded)
            return;

        e.Handled = true;

        var vm = DataContext as MainViewModel;
        if (vm is not null)
            await vm.LoadWorkspaceItemsAsync(node);
    }

    // ------------------------------------------------------------------
    // TreeView: sync selected workspace with ViewModel (informational)
    // ------------------------------------------------------------------

    private void WorkspaceTreeView_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        // Selection is informational only — backup uses checked items
    }

    // ------------------------------------------------------------------
    // Hyperlink: open mailto in default mail client
    // ------------------------------------------------------------------

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // ------------------------------------------------------------------
    // CheckBox click: skip indeterminate state, refresh selection info
    // ------------------------------------------------------------------

    private void TreeNodeCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            // Skip the indeterminate (null) cycle: null → false
            if (cb.IsChecked == null)
                cb.IsChecked = false;
        }

        (DataContext as MainViewModel)?.RefreshSelectionInfo();

        // Prevent the click from bubbling to the TreeViewItem (avoids expand/collapse)
        e.Handled = true;
    }
}
