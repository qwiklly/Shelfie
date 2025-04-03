using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MauiApp1.Models;
using Microsoft.Extensions.Logging;

public class AuthService
{
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

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>(options);

                if (result?.Flag == true && !string.IsNullOrEmpty(result.JWTToken))
                {
                    await SecureStorage.Default.SetAsync("auth_token", result.JWTToken);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке запроса к API");
            throw;
        }

    }

    public async Task LogoutAsync()
    {
        try
        {
            SecureStorage.Default.Remove("auth_token");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке запроса к API");
            throw;
        }
    }
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync("auth_token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении токена");
            throw;
        }
    }

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

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке запроса к API");
            throw;
        }
    }

    public async Task<string> GetUserEmailAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return "Email not found";

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email);
            return emailClaim?.Value ?? "Email not found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке запроса к API");
            throw new Exception("Ошибка при получении данных пользователя", ex);
        }
    }
    public async Task<UserProfileModel?> GetUserDataAsync()
    {
        try
        {
            var email = await GetUserEmailAsync();
            if (string.IsNullOrEmpty(email))
                return null;

            return await _httpClient.GetFromJsonAsync<UserProfileModel>($"application/getUser?email={email}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка загрузки данных пользователя: {ex.Message}");
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

            var response = await _httpClient.DeleteAsync($"application/deleteUser?email={email}");
            return response.IsSuccessStatusCode;

        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка удаления аккаунта: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(string email, string name, string phone, string? password = null)
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return false;

            var updateRequest = new RegisterRequest
            {
                Name = name,
                Phone = phone,
                Password = password ?? string.Empty
            };

            var request = new HttpRequestMessage(HttpMethod.Put, $"application/updateUser/{email}")
            {
                Content = JsonContent.Create(updateRequest)
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении данных аккаунта"); ;
            return false;
        }
    }
}