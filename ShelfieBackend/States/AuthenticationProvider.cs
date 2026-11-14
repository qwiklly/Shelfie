using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ShelfieBackend.DTOs;
using Serilog;

namespace ShelfieBackend.States
{
    public class AuthenticationProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal anonymous = new(new ClaimsIdentity());

        public AuthenticationProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var jwtToken = GetJwtTokenFromCookie();
                if (string.IsNullOrEmpty(jwtToken))
                    return await Task.FromResult(new AuthenticationState(anonymous));

                var getUserClaims = DecryptToken(jwtToken);
                if (getUserClaims == null)
                    return await Task.FromResult(new AuthenticationState(anonymous));

                var claimsPrincipal = SetClaimPrincipal(getUserClaims);
                return await Task.FromResult(new AuthenticationState(claimsPrincipal));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while retrieving authentication state");
                return new AuthenticationState(anonymous);
            }
        }

        public async Task UpdateAuthenticationState(string? jwtToken)
        {
            var claimsPrincipal = new ClaimsPrincipal();
            if (!string.IsNullOrEmpty(jwtToken))
            {
                var getUserClaims = DecryptToken(jwtToken);
                claimsPrincipal = SetClaimPrincipal(getUserClaims);
                await SetJwtTokenInCookie(jwtToken);
            }
            else
            {
                RemoveJwtTokenFromCookie();
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }

        private Task SetJwtTokenInCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {

                HttpOnly = true,
                Secure = _httpContextAccessor.HttpContext?.Request.IsHttps == true,
                Expires = DateTime.UtcNow.AddDays(2),
                SameSite = SameSiteMode.Lax // http -> https
            };
            _httpContextAccessor.HttpContext?.Response.Cookies.Append(Constants.AuthCookieName, token, cookieOptions);
            return Task.CompletedTask;

        }

        private string? GetJwtTokenFromCookie()
        {
            if (_httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(Constants.AuthCookieName, out string? token) == true)
            {
                return token;
            }

            return null;
        }

        private void RemoveJwtTokenFromCookie()
        {
            if (_httpContextAccessor.HttpContext?.Request.Cookies.ContainsKey(Constants.AuthCookieName) == true)
            {
                _httpContextAccessor.HttpContext.Response.Cookies.Delete(Constants.AuthCookieName);
            }
        }

        private static ClaimsPrincipal SetClaimPrincipal(CustomUserClaims claims)
        {
            if (claims.Email is null) return new ClaimsPrincipal();
            return new ClaimsPrincipal(new ClaimsIdentity(
                new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, claims.Id),
                    new(ClaimTypes.Name, claims.Name!),
                    new(ClaimTypes.Email, claims.Email!),
                    new(ClaimTypes.Role, claims.Role.ToString()),
                }, "JwtAuth"));
        }

        private static CustomUserClaims DecryptToken(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken)) return new CustomUserClaims();
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);
            var id = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.NameIdentifier);
            var name = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Name);
            var email = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Email);
            var role = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Role);

            if (name == null || email == null || role == null || id == null)
                return new CustomUserClaims();

            return new CustomUserClaims(
                id.Value,
                name!.Value,
                email!.Value,
                Enum.TryParse<UserRole>(role!.Value, out var parsedRole) ? parsedRole : UserRole.User
            );
        }
    }
}