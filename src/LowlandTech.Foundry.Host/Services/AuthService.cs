using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LowlandTech.Foundry.Host.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ILogger<AuthService> _logger;

    private const string AuthDataKey = "foundry_auth_data";
    private const string StorageTypeKey = "foundry_auth_storage_type";

    // Token refresh buffer - refresh 5 minutes before expiry
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    // Cache for the current circuit
    private AuthData? _cachedAuthData;
    private bool _initialized;
    private bool _usePersistentStorage;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ProtectedSessionStorage sessionStorage,
        ProtectedLocalStorage localStorage,
        ILogger<AuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _sessionStorage = sessionStorage;
        _localStorage = localStorage;
        _logger = logger;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("api");
        if (_cachedAuthData?.AccessToken != null)
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cachedAuthData.AccessToken);
        }
        return client;
    }

    private async Task SaveAuthDataAsync(AuthData authData, bool persistent)
    {
        _cachedAuthData = authData;
        _usePersistentStorage = persistent;

        try
        {
            if (persistent)
            {
                await _localStorage.SetAsync(AuthDataKey, authData);
                await _localStorage.SetAsync(StorageTypeKey, "persistent");
            }
            else
            {
                await _sessionStorage.SetAsync(AuthDataKey, authData);
                await _sessionStorage.SetAsync(StorageTypeKey, "session");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save auth data to storage");
        }
    }

    private async Task ClearAuthDataAsync()
    {
        _cachedAuthData = null;
        _initialized = false;

        try
        {
            // Clear from both storages
            await _sessionStorage.DeleteAsync(AuthDataKey);
            await _sessionStorage.DeleteAsync(StorageTypeKey);
            await _localStorage.DeleteAsync(AuthDataKey);
            await _localStorage.DeleteAsync(StorageTypeKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear auth data from storage");
        }
    }

    private async Task<AuthData?> LoadAuthDataAsync()
    {
        try
        {
            // First check local storage for persistent session
            var localResult = await _localStorage.GetAsync<AuthData>(AuthDataKey);
            if (localResult.Success && localResult.Value != null)
            {
                _usePersistentStorage = true;
                return localResult.Value;
            }

            // Then check session storage
            var sessionResult = await _sessionStorage.GetAsync<AuthData>(AuthDataKey);
            if (sessionResult.Success && sessionResult.Value != null)
            {
                _usePersistentStorage = false;
                return sessionResult.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load auth data from storage");
        }

        return null;
    }

    private bool IsTokenExpiringSoon()
    {
        if (_cachedAuthData?.ExpiresAt == null)
            return true;

        return DateTime.UtcNow.Add(TokenRefreshBuffer) >= _cachedAuthData.ExpiresAt;
    }

    private async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_cachedAuthData?.RefreshToken))
        {
            _logger.LogDebug("No refresh token available");
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("api");
            var response = await client.PostAsJsonAsync("/refresh", new RefreshRequest(_cachedAuthData.RefreshToken));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenResponse?.AccessToken != null)
                {
                    var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    var authData = new AuthData(
                        tokenResponse.AccessToken,
                        tokenResponse.RefreshToken ?? _cachedAuthData.RefreshToken,
                        _cachedAuthData.Email,
                        expiresAt
                    );

                    await SaveAuthDataAsync(authData, _usePersistentStorage);
                    _logger.LogInformation("Token refreshed successfully, expires at {ExpiresAt}", expiresAt);
                    return true;
                }
            }

            _logger.LogWarning("Token refresh failed with status {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
        }

        return false;
    }

    private async Task EnsureValidTokenAsync()
    {
        if (_cachedAuthData == null)
            return;

        if (IsTokenExpiringSoon())
        {
            _logger.LogDebug("Token expiring soon, attempting refresh");
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                _logger.LogWarning("Token refresh failed, user may need to re-authenticate");
            }
        }
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe = false)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("api");
            var response = await client.PostAsJsonAsync("/login", new LoginRequest(email, password));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Login response received");

                try
                {
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenResponse?.AccessToken != null)
                    {
                        var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                        var authData = new AuthData(
                            tokenResponse.AccessToken,
                            tokenResponse.RefreshToken,
                            email,
                            expiresAt
                        );

                        await SaveAuthDataAsync(authData, rememberMe);
                        _initialized = true;

                        _logger.LogInformation(
                            "Login successful for {Email}, token expires at {ExpiresAt}, persistent: {Persistent}",
                            email, expiresAt, rememberMe);

                        return new AuthResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token parsing failed");
                }

                // Fallback if token parsing fails
                var fallbackAuthData = new AuthData(null, null, email, null);
                await SaveAuthDataAsync(fallbackAuthData, rememberMe);
                _initialized = true;
                return new AuthResult(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Login failed: {Error}", error);
            return new AuthResult(false, "Invalid email or password");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return new AuthResult(false, "An error occurred during login");
        }
    }

    public async Task<AuthResult> RegisterAsync(string email, string password)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("api");
            var response = await client.PostAsJsonAsync("/register", new RegisterRequest(email, password));

            if (response.IsSuccessStatusCode)
            {
                return new AuthResult(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Registration failed: {Error}", error);

            try
            {
                var errorResponse = JsonSerializer.Deserialize<IdentityErrorResponse>(error, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorResponse?.Errors != null)
                {
                    var errorMessages = errorResponse.Errors
                        .SelectMany(e => e.Value)
                        .ToList();

                    if (errorMessages.Any())
                    {
                        return new AuthResult(false, string.Join(" ", errorMessages));
                    }
                }
            }
            catch { }

            return new AuthResult(false, "Registration failed. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            return new AuthResult(false, "An error occurred during registration");
        }
    }

    public async Task LogoutAsync()
    {
        // Call the API logout endpoint first
        try
        {
            var client = CreateAuthenticatedClient();
            await client.PostAsync("/api/auth/logout", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call logout endpoint");
        }

        await ClearAuthDataAsync();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        // Return cached value if already initialized
        if (_initialized && _cachedAuthData != null)
        {
            // Check if token needs refresh
            await EnsureValidTokenAsync();
            return new UserInfo(true, _cachedAuthData.Email, []);
        }

        // Try to restore from storage
        var authData = await LoadAuthDataAsync();
        if (authData != null)
        {
            _cachedAuthData = authData;
            _initialized = true;

            // Check if token needs refresh
            await EnsureValidTokenAsync();
            return new UserInfo(true, authData.Email, []);
        }

        _initialized = true;
        return null;
    }

    public async Task<UserProfile?> GetProfileAsync()
    {
        try
        {
            await GetCurrentUserAsync();
            await EnsureValidTokenAsync();

            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/profile");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserProfile>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            _logger.LogWarning("Failed to get profile: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile");
            return null;
        }
    }

    public async Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            await GetCurrentUserAsync();
            await EnsureValidTokenAsync();

            var client = CreateAuthenticatedClient();
            var response = await client.PutAsJsonAsync("/api/profile", request);

            if (response.IsSuccessStatusCode)
            {
                return new AuthResult(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Profile update failed: {Error}", error);

            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(error, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorResponse?.Errors != null && errorResponse.Errors.Any())
                {
                    return new AuthResult(false, string.Join(" ", errorResponse.Errors));
                }
            }
            catch { }

            return new AuthResult(false, "Failed to update profile");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return new AuthResult(false, "An error occurred while updating profile");
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            await GetCurrentUserAsync();
            await EnsureValidTokenAsync();

            var client = CreateAuthenticatedClient();
            var response = await client.PostAsJsonAsync("/api/profile/change-password",
                new ChangePasswordRequest(currentPassword, newPassword));

            if (response.IsSuccessStatusCode)
            {
                return new AuthResult(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Password change failed: {Error}", error);

            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(error, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorResponse?.Errors != null && errorResponse.Errors.Any())
                {
                    return new AuthResult(false, string.Join(" ", errorResponse.Errors));
                }
            }
            catch { }

            return new AuthResult(false, "Failed to change password");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return new AuthResult(false, "An error occurred while changing password");
        }
    }
}

// Auth data stored in browser storage
internal record AuthData(
    string? AccessToken,
    string? RefreshToken,
    string? Email,
    DateTime? ExpiresAt
);

// Token response from Identity API
internal record TokenResponse(
    string? TokenType,
    string? AccessToken,
    int ExpiresIn,
    string? RefreshToken
);

// Refresh token request
internal record RefreshRequest(string RefreshToken);

// Identity error response
internal record IdentityErrorResponse(
    string? Type,
    string? Title,
    int Status,
    Dictionary<string, string[]>? Errors
);

// API error response
internal record ApiErrorResponse(string[]? Errors);
