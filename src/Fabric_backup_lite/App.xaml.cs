using System.IO;
using System.Windows;
using Fabric_backup_lite.Services;
using Fabric_backup_lite.ViewModels;
using Fabric_backup_lite.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Fabric_backup_lite;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Logs go to %APPDATA%\WeData\FabricBackupLite\logs\ so they are
        // accessible even when the app runs from Program Files (read-only).
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WeData", "FabricBackupLite", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(logDir, "fabric-backup-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Application starting up");

            // Build service provider
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Log.Information("Main window displayed");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            MessageBox.Show(
                $"Application failed to start:\n\n{ex.Message}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.AddSingleton(_configuration!);

        // Logging
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(dispose: true);
        });

        // Services
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IFabricApiClient, FabricApiClient>();
        services.AddTransient<IBackupService, BackupService>();
        services.AddSingleton<FileSystemService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down");
        Log.CloseAndFlush();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
