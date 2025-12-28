using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LowlandTech.Foundry.Host.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ILogger<AuthService> _logger;

    private const string TokenStorageKey = "foundry_auth_token";
    private const string UserStorageKey = "foundry_auth_user";

    // Cache for the current request
    private UserInfo? _cachedUser;
    private string? _cachedToken;
    private bool _initialized;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ProtectedSessionStorage sessionStorage,
        ILogger<AuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("api");
        if (!string.IsNullOrEmpty(_cachedToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cachedToken);
        }
        return client;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("api");
            var response = await client.PostAsJsonAsync("/login", new LoginRequest(email, password));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Login response: {Content}", content);

                try
                {
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenResponse?.AccessToken != null)
                    {
                        _cachedToken = tokenResponse.AccessToken;
                        _cachedUser = new UserInfo(true, email, []);

                        // Persist to session storage
                        await _sessionStorage.SetAsync(TokenStorageKey, _cachedToken);
                        await _sessionStorage.SetAsync(UserStorageKey, _cachedUser);

                        _initialized = true;
                        return new AuthResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token parsing failed");
                }

                // Fallback if token parsing fails
                _cachedUser = new UserInfo(true, email, []);
                await _sessionStorage.SetAsync(UserStorageKey, _cachedUser);
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

            // Try to parse validation errors
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
        _cachedUser = null;
        _cachedToken = null;
        _initialized = false;

        try
        {
            await _sessionStorage.DeleteAsync(TokenStorageKey);
            await _sessionStorage.DeleteAsync(UserStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear session storage");
        }

        // Also call the API logout endpoint to invalidate server-side session
        try
        {
            var client = CreateAuthenticatedClient();
            await client.PostAsync("/api/auth/logout", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call logout endpoint");
        }
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        // Return cached value if already initialized
        if (_initialized)
        {
            return _cachedUser;
        }

        // Try to restore from session storage
        try
        {
            var userResult = await _sessionStorage.GetAsync<UserInfo>(UserStorageKey);
            if (userResult.Success && userResult.Value != null)
            {
                _cachedUser = userResult.Value;

                var tokenResult = await _sessionStorage.GetAsync<string>(TokenStorageKey);
                if (tokenResult.Success)
                {
                    _cachedToken = tokenResult.Value;
                }

                _initialized = true;
                return _cachedUser;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore auth state from session storage");
        }

        _initialized = true;
        return null;
    }
}

// Token response from Identity API
internal record TokenResponse(
    string? TokenType,
    string? AccessToken,
    int ExpiresIn,
    string? RefreshToken
);

// Identity error response
internal record IdentityErrorResponse(
    string? Type,
    string? Title,
    int Status,
    Dictionary<string, string[]>? Errors
);
