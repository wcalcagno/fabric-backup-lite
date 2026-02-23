namespace Fabric_backup_lite.Services;

public interface IAuthenticationService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<string> GetTenantIdAsync();
    Task SignInAsync();
    Task SignOutAsync();
    bool IsAuthenticated { get; }
    string? UserDisplayName { get; }
}
