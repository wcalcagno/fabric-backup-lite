using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fabric_backup_lite.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IPublicClientApplication _msalClient;
    private readonly string[] _scopes;
    private readonly ILogger<AuthenticationService> _logger;
    private IAccount? _currentAccount;

    public bool IsAuthenticated => _currentAccount != null;
    public string? UserDisplayName => _currentAccount?.Username;

    public AuthenticationService(IConfiguration configuration, ILogger<AuthenticationService> logger)
    {
        _logger = logger;

        var clientId = configuration["Authentication:ClientId"]
            ?? throw new InvalidOperationException("ClientId not configured");
        var tenantId = configuration["Authentication:TenantId"] ?? "common";
        var redirectUri = configuration["Authentication:RedirectUri"] ?? "http://localhost";

        var scopesConfig = configuration.GetSection("Authentication:Scopes").Get<string[]>();
        _scopes = scopesConfig ?? new[] { "https://api.fabric.microsoft.com/.default" };

        _msalClient = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithRedirectUri(redirectUri)
            .WithLogging((level, message, containsPii) =>
            {
                if (!containsPii)
                {
                    _logger.LogDebug("[MSAL] {Message}", message);
                }
            }, Microsoft.Identity.Client.LogLevel.Verbose, enablePiiLogging: false, enableDefaultPlatformLogging: true)
            .Build();
    }

    public async Task SignInAsync()
    {
        try
        {
            _logger.LogInformation("Attempting interactive sign-in");

            var accounts = await _msalClient.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            AuthenticationResult result;

            try
            {
                // Intentar adquirir token silenciosamente primero
                result = await _msalClient
                    .AcquireTokenSilent(_scopes, firstAccount)
                    .ExecuteAsync();

                _logger.LogInformation("Successfully acquired token silently");
            }
            catch (MsalUiRequiredException)
            {
                // Si falla, hacer login interactivo
                _logger.LogInformation("Silent token acquisition failed, starting interactive login");

                result = await _msalClient
                    .AcquireTokenInteractive(_scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();

                _logger.LogInformation("Successfully acquired token interactively");
            }

            _currentAccount = result.Account;
            _logger.LogInformation("Signed in as {Username}", _currentAccount.Username);
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "MSAL authentication failed: {ErrorCode}", ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed");
            throw;
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_currentAccount == null)
        {
            throw new InvalidOperationException("Not authenticated. Call SignInAsync first.");
        }

        try
        {
            var result = await _msalClient
                .AcquireTokenSilent(_scopes, _currentAccount)
                .ExecuteAsync(cancellationToken);

            _logger.LogDebug("Access token acquired (expires: {ExpiresOn})", result.ExpiresOn);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogWarning("Token expired, re-authentication required");
            await SignInAsync();
            return await GetAccessTokenAsync(cancellationToken);
        }
    }

    public async Task<string> GetTenantIdAsync()
    {
        if (_currentAccount == null)
        {
            throw new InvalidOperationException("Not authenticated.");
        }

        return await Task.FromResult(_currentAccount.HomeAccountId.TenantId);
    }

    public async Task SignOutAsync()
    {
        if (_currentAccount != null)
        {
            await _msalClient.RemoveAsync(_currentAccount);
            _currentAccount = null;
            _logger.LogInformation("User signed out");
        }
    }
}
