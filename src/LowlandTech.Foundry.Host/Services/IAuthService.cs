namespace LowlandTech.Foundry.Host.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe = false);
    Task<AuthResult> RegisterAsync(string email, string password);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<UserProfile?> GetProfileAsync();
    Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request);
    Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword);
}

public record AuthResult(bool Success, string? Error = null);

public record UserInfo(bool IsAuthenticated, string? Name, ClaimInfo[] Claims);

public record ClaimInfo(string Type, string Value);

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Email, string Password);

// Profile DTOs
public record UserProfile(
    string? Email,
    string? UserName,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PhoneNumber,
    AddressInfo? Address,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AddressInfo(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);

public record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PhoneNumber,
    UpdateAddressRequest? Address
);

public record UpdateAddressRequest(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
