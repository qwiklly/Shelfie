using ShelfieBackend.DTOs;
using ShelfieBackend.States;
using static ShelfieBackend.Responses.CustomResponses;
using System.Text.Json;

namespace ShelfieBackend.Services
{
    public class AccountService(HttpClient _httpClient) : IAccountService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static async Task<T> SendRequestAsync<T>(Func<CancellationToken, Task<HttpResponseMessage>> httpRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await httpRequest(cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return default!;
                }

                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false) ??
                    throw new HttpRequestException("Empty or invalid JSON response received.");
            }
            catch (TaskCanceledException)
            {
                throw new HttpRequestException("Request timed out.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"HTTP error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
                throw new HttpRequestException("Unexpected error occurred.", ex);
            }
        }

        public Task<BaseResponse> GetUsersAsync(CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.GetAsync(ApiEndpoints.GetUsers, ct), cancellationToken);

        public Task<BaseResponse> GetUserAsync(string email, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.GetAsync($"{ApiEndpoints.GetUser}{Uri.EscapeDataString(email)}", ct), cancellationToken);

        public Task<LoginResponse> LoginAsync(LoginDTO model, CancellationToken cancellationToken = default) =>
            SendRequestAsync<LoginResponse>((ct) => _httpClient.PostAsJsonAsync(ApiEndpoints.Login, model, ct), cancellationToken);

        public Task<BaseResponse> DeleteUserAsync(string email, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.DeleteAsync($"{ApiEndpoints.DeleteUser}{Uri.EscapeDataString(email)}", ct), cancellationToken);

        public Task<BaseResponse> UpdateUserAsync(RegisterDTO model, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.PutAsJsonAsync(ApiEndpoints.UpdateUser, model, ct), cancellationToken);

        public Task<RegisterResponse> RegisterAsync(RegisterDTO model, CancellationToken cancellationToken = default) =>
            SendRequestAsync<RegisterResponse>((ct) => _httpClient.PostAsJsonAsync(ApiEndpoints.Register, model, ct), cancellationToken);
    }
}