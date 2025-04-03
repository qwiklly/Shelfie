using System.Net.Http.Headers;
using System.Net.Http.Json;
using MauiApp1.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Services
{
    public class ProductService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILogger<ProductService> _logger;

        public ProductService(HttpClient httpClient, AuthService authService, ILogger<ProductService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _logger = logger;
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, object? content = null)
        {
            var token = await _authService.GetTokenAsync();
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            if (content != null)
            {
                request.Content = JsonContent.Create(content);
            }
            return request;
        }

        public async Task<List<ProductModel>> GetProductsAsync()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Get, "application/getAll");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ProductModel>>(_jsonOptions) ?? new List<ProductModel>();
                }
                return new List<ProductModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка продуктов");
                throw;
            }
        }

        public async Task<bool> AddProductAsync(string name, string creator, DateOnly expirationDate, int quantity, double weight, string weightUnit)
        {
            try
            {
                var requestData = new AddProductRequest
                {
                    Name = name,
                    Creator = creator,
                    CategoryId = 1,
                    ExpirationDate = expirationDate,
                    Quantity = quantity,
                    Weight = weight,
                    WeightUnit = weightUnit
                };

                var request = await CreateRequestAsync(HttpMethod.Post, "application/add", requestData);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении продукта");
                return false;
            }
        }

        public async Task<bool> UpdateProductAsync(string currentName, string newName, string newCreator, DateOnly? expirationDate, int? quantity, double? weight, string weightUnit)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentName))
                    throw new ArgumentNullException(nameof(currentName), "Имя текущего продукта не может быть пустым");

                var requestData = new UpdateProductRequest
                {
                    Name = newName,
                    Creator = newCreator,
                    ExpirationDate = expirationDate,
                    Quantity = quantity,
                    Weight = weight,
                    WeightUnit = weightUnit
                };

                var request = await CreateRequestAsync(HttpMethod.Put, $"application/update/{Uri.EscapeDataString(currentName)}", requestData);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении продукта");
                return false;
            }
        }

        public async Task<bool> DeleteProductAsync(string productName)
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Delete, $"application/delete/{Uri.EscapeDataString(productName)}");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении продукта");
                return false;
            }
        }

        public async Task<List<HistoryRecordModel>> GetProductHistoryAsync()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Get, "application/getAllHistory");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<HistoryRecordModel>>(_jsonOptions) ?? new List<HistoryRecordModel>();
                }
                return new List<HistoryRecordModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении истории продуктов");
                throw;
            }
            
        }

        public async Task<bool> DeleteHistoryAsync()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Delete, "application/clear");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке истории продуктов");
                return false;
            }
        }
        public async Task<List<ProductModel>> GetExpiredProducts()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Get, "application/getExpiredProducts");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ProductModel>>(_jsonOptions) ?? new List<ProductModel>();
                }
                return new List<ProductModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка продуктов");
                throw;
            }
        }
    }
}
