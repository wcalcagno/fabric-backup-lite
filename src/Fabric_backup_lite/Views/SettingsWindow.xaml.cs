using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Fabric_backup_lite.Services;
using Microsoft.Extensions.Configuration;

namespace Fabric_backup_lite.Views;

public partial class SettingsWindow : Window
{
    private readonly string _userSettingsPath;

    public SettingsWindow(IConfiguration configuration)
    {
        InitializeComponent();

        _userSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WeData", "FabricBackupLite", "usersettings.json");

        ApplyLocalization();

        ClientIdBox.Text = configuration["Authentication:ClientId"] ?? string.Empty;
        TenantIdBox.Text = configuration["Authentication:TenantId"] ?? string.Empty;
    }

    private void ApplyLocalization()
    {
        var l = LocalizationService.Instance;

        SubtitleBlock.Text     = l.SettingsSubtitle;
        AuthHeaderBlock.Text   = l.SettingsAuthSection;
        ClientIdLabelBlock.Text = l.ClientIdLabel;
        TenantIdLabelBlock.Text = l.TenantIdLabel;
        HintBlock.Text         = l.SettingsHint;
        SaveBtn.Content        = l.SaveSettingsButton;
        CloseBtn.Content       = l.CloseSettingsButton;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new
        {
            Authentication = new
            {
                ClientId = ClientIdBox.Text.Trim(),
                TenantId = TenantIdBox.Text.Trim()
            }
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(_userSettingsPath)!);
        File.WriteAllText(_userSettingsPath, json, Encoding.UTF8);

        var l = LocalizationService.Instance;
        MessageBox.Show(
            l.SettingsSavedMsg,
            l.SettingsSavedTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
