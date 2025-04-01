using ShelfieBackend.DTOs;
using ShelfieBackend.States;
using static ShelfieBackend.Responses.CustomResponses;
using System.Text.Json;
using ShelfieBackend.Models;

namespace ShelfieBackend.Services
{
    public class ProductService(HttpClient _httpClient) : IProductService
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

        public Task<BaseResponse> GetProductsAsync(CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.GetAsync(ApiEndpoints.GetProducts, ct), cancellationToken);

        public Task<BaseResponse> GetProductAsync(string name, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.GetAsync($"{ApiEndpoints.GetProduct}{Uri.EscapeDataString(name)}", ct), cancellationToken);

        public Task<BaseResponse> AddProductAsync(AddProductDTO model, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.PostAsJsonAsync(ApiEndpoints.AddProduct, model, ct), cancellationToken);

        public Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.PutAsJsonAsync($"{ApiEndpoints.UpdateProduct}/{Uri.EscapeDataString(currentName)}", model, ct), cancellationToken);


        public Task<BaseResponse> DeleteProductAsync(string name, CancellationToken cancellationToken = default) =>
            SendRequestAsync<BaseResponse>((ct) => _httpClient.DeleteAsync($"{ApiEndpoints.DeleteProduct}{Uri.EscapeDataString(name)}", ct), cancellationToken);

        public Task<List<HistoryRecord>> GetAllHistoryAsync(CancellationToken cancellationToken = default) =>
            SendRequestAsync<List<HistoryRecord>>((ct) => _httpClient.GetAsync(ApiEndpoints.GetAllHistory, ct), cancellationToken);

    }
}
