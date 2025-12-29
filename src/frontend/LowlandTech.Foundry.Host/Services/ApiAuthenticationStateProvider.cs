using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LowlandTech.Foundry.Host.Services;

public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthService _authService;
    private readonly ILogger<ApiAuthenticationStateProvider> _logger;

    public ApiAuthenticationStateProvider(IAuthService authService, ILogger<ApiAuthenticationStateProvider> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userInfo = await _authService.GetCurrentUserAsync();

            if (userInfo?.IsAuthenticated == true)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, userInfo.Name ?? "User")
                };

                // Add all claims from the API
                foreach (var claim in userInfo.Claims)
                {
                    claims.Add(new Claim(claim.Type, claim.Value));
                }

                var identity = new ClaimsIdentity(claims, "ApiAuth");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
