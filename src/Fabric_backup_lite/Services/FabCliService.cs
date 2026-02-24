using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.Services;

public class FabCliService : IFabCliService
{
    private readonly ILogger<FabCliService> _logger;
    private bool? _isAvailableCache;

    public FabCliService(ILogger<FabCliService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailableCache.HasValue)
            return _isAvailableCache.Value;

        try
        {
            var result = await RunCommandAsync("fab", "--version", cancellationToken);
            _isAvailableCache = result.ExitCode == 0;

            if (_isAvailableCache.Value)
                _logger.LogInformation("Fabric CLI detected: {Output}", result.Output.Trim());
            else
                _logger.LogDebug("Fabric CLI not available (exit code {Code})", result.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Fabric CLI not found: {Msg}", ex.Message);
            _isAvailableCache = false;
        }

        return _isAvailableCache.Value;
    }

    public async Task<string?> ExportItemAsync(
        string workspaceName,
        string itemName,
        string itemType,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            _logger.LogWarning("Fabric CLI not available; skipping fab export for {Item}", itemName);
            return null;
        }

        // fab export syntax: fab export "{workspace}.Workspace/{item}.{Type}" -o "{output}" --force
        var itemPath  = $"{workspaceName}.Workspace/{itemName}.{itemType}";
        var arguments = $"export \"{itemPath}\" -o \"{outputDirectory}\" --force";

        _logger.LogInformation("Running: fab {Args}", arguments);

        var result = await RunCommandAsync("fab", arguments, cancellationToken);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("fab export succeeded for {Item}", itemName);
            // fab creates a subfolder named after the item inside outputDirectory
            var expectedPath = Path.Combine(outputDirectory, $"{itemName}.{itemType}");
            return Directory.Exists(expectedPath) ? expectedPath : outputDirectory;
        }

        _logger.LogWarning(
            "fab export failed for {Item} (exit {Code}): {Error}",
            itemName, result.ExitCode, result.Error);
        return null;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask  = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await outputTask, await errorTask);
    }
}
