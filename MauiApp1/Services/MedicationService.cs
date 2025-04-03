using System.Net.Http.Headers;
using System.Net.Http.Json;
using MauiApp1.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Services
{
    public class MedicationService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILogger<ProductService> _logger;

        public MedicationService(HttpClient httpClient, AuthService authService, ILogger<ProductService> logger)
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

        public async Task<List<MedicineModel>> GetMedicineAsync()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Get, "medication/getAll");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<MedicineModel>>(_jsonOptions) ?? new List<MedicineModel>();
                }
                return new List<MedicineModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка продуктов");
                throw;
            }
        }

        public async Task<bool> AddMedicineAsync(string name, string creator, DateOnly expirationDate, int quantity, double? weight, string weightUnit)
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
                    Weight = weight ?? 0,
                    WeightUnit = weightUnit
                };

                var request = await CreateRequestAsync(HttpMethod.Post, "medication/add", requestData);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении продукта");
                return false;
            }
        }

        public async Task<bool> UpdateMedicineAsync(string currentName, string newName, string newCreator, DateOnly? expirationDate, int? quantity, double? weight, string weightUnit)
        {
            try
            {
                var requestData = new UpdateProductRequest
                {
                    Name = newName,
                    Creator = newCreator,
                    ExpirationDate = expirationDate,
                    Quantity = quantity,
                    Weight = weight,
                    WeightUnit = weightUnit
                };

                var request = await CreateRequestAsync(HttpMethod.Put, $"medication/update/{Uri.EscapeDataString(currentName)}", requestData);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении продукта");
                return false;
            }
        }

        public async Task<bool> DeleteMedicineAsync(string productName)
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Delete, $"medication/delete/{Uri.EscapeDataString(productName)}");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении продукта");
                return false;
            }
        }
        public async Task<List<MedicineModel>> GetExpiredMedicineAsync()
        {
            try
            {
                var request = await CreateRequestAsync(HttpMethod.Get, "medication/getExpiredMedicaments");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<MedicineModel>>(_jsonOptions) ?? new List<MedicineModel>();
                }
                return new List<MedicineModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка продуктов");
                throw;
            }
        }
    }
}