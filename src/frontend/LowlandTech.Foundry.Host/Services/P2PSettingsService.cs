using System.Net.Http.Json;
using System.Text.Json;

namespace LowlandTech.Foundry.Host.Services;

public class P2PSettingsService : IP2PSettingsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<P2PSettingsService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public P2PSettingsService(
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILogger<P2PSettingsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    private HttpClient CreateClient()
    {
        return _httpClientFactory.CreateClient("api");
    }

    public async Task<UserP2PSettingsDto?> GetSettingsAsync()
    {
        try
        {
            // Ensure user is authenticated
            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                _logger.LogWarning("Cannot get P2P settings: user not authenticated");
                return null;
            }

            var client = CreateClient();
            var response = await client.GetAsync("/api/p2p-settings");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserP2PSettingsDto>(JsonOptions);
            }

            _logger.LogWarning("Failed to get P2P settings: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting P2P settings");
            return null;
        }
    }

    public async Task<bool> UpdateSettingsAsync(UpdateP2PSettingsDto settings)
    {
        try
        {
            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                _logger.LogWarning("Cannot update P2P settings: user not authenticated");
                return false;
            }

            var client = CreateClient();
            var response = await client.PutAsJsonAsync("/api/p2p-settings", settings);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("P2P settings updated successfully");
                return true;
            }

            _logger.LogWarning("Failed to update P2P settings: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating P2P settings");
            return false;
        }
    }

    public async Task<bool> TrustPeerAsync(string peerId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/p2p-settings/trust", new { PeerId = peerId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trusting peer {PeerId}", peerId);
            return false;
        }
    }

    public async Task<bool> BlockPeerAsync(string peerId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/p2p-settings/block", new { PeerId = peerId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking peer {PeerId}", peerId);
            return false;
        }
    }

    public async Task<bool> UntrustPeerAsync(string peerId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.DeleteAsync($"/api/p2p-settings/trust/{Uri.EscapeDataString(peerId)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error untrusting peer {PeerId}", peerId);
            return false;
        }
    }

    public async Task<bool> UnblockPeerAsync(string peerId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.DeleteAsync($"/api/p2p-settings/block/{Uri.EscapeDataString(peerId)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking peer {PeerId}", peerId);
            return false;
        }
    }
}
