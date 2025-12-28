namespace LowlandTech.Foundry.Host.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterAsync(string email, string password);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
}

public record AuthResult(bool Success, string? Error = null);

public record UserInfo(bool IsAuthenticated, string? Name, ClaimInfo[] Claims);

public record ClaimInfo(string Type, string Value);

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Email, string Password);
