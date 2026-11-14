using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Extensions.Logging;

public class AuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public AuthService(HttpClient httpClient, ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync("application/login", loginRequest);

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (result?.Flag == true && !string.IsNullOrEmpty(result.JWTToken))
            {
                await TokenStore.SaveAsync(result.JWTToken);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при входе");
            return false;
        }
    }

    public Task LogoutAsync()
    {
        TokenStore.Clear();
        return Task.CompletedTask;
    }

    /// <summary>The stored token, or null when the user is not logged in or the token has expired.</summary>
    public Task<string?> GetTokenAsync() => TokenStore.GetValidTokenAsync();

    public async Task<bool> RegisterAsync(string email, string password, string confirmPassword, string name, int role, string phone, DateOnly dateOfBirth)
    {
        try
        {
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = confirmPassword,
                Name = name,
                Role = role,
                Phone = phone,
                DateOfBirth = dateOfBirth
            };
            var response = await _httpClient.PostAsJsonAsync("application/register", registerRequest);

            // The API now answers 400 for "user exists", weak password etc. — previously it was always 200.
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации");
            return false;
        }
    }

    /// <summary>E-mail from the stored token, or null when not logged in.</summary>
    public async Task<string?> GetUserEmailAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return null;

        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
    }

    public async Task<UserProfileModel?> GetUserDataAsync()
    {
        try
        {
            var email = await GetUserEmailAsync();
            if (string.IsNullOrEmpty(email))
                return null;

            return await _httpClient.GetFromJsonAsync<UserProfileModel>(
                $"application/getUser?email={Uri.EscapeDataString(email)}", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки данных пользователя");
            return null;
        }
    }

    public async Task<bool> DeleteAccountAsync()
    {
        try
        {
            var email = await GetUserEmailAsync();
            if (string.IsNullOrEmpty(email))
                return false;

            var response = await _httpClient.DeleteAsync($"application/deleteUser?email={Uri.EscapeDataString(email)}");
            if (!response.IsSuccessStatusCode)
                return false;

            // The account is gone — the token must not keep the app "logged in".
            await LogoutAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления аккаунта");
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(string email, string name, string phone, string? password = null)
    {
        try
        {
            // Only the fields that may change. Sending a full RegisterRequest used to send Role = 0,
            // which demoted admins and made the update fail for workers.
            var updateRequest = new UpdateUserRequest
            {
                Name = name,
                Phone = phone,
                Password = string.IsNullOrEmpty(password) ? null : password
            };

            var response = await _httpClient.PutAsJsonAsync($"application/updateUser/{Uri.EscapeDataString(email)}", updateRequest);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении данных аккаунта");
            return false;
        }
    }
}
