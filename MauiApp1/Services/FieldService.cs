using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace MauiApp1.Services
{
    public class FieldService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FieldService> _logger;

        public FieldService(HttpClient httpClient, ILogger<FieldService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<string>> GetFieldsForCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"category/{Uri.EscapeDataString(categoryId.ToString())}/getCategoryFields";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при получении полей категории {categoryId}: {response.StatusCode}");
                    return [];
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fields = await response.Content.ReadFromJsonAsync<List<string>>(options, cancellationToken);
                return fields ?? [];
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при получении полей категории {categoryId}");
                return [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Ошибка парсинга JSON при получении полей категории {categoryId}");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при получении полей категории {categoryId}");
                throw;
            }
        }

        /// <summary>Fields with their server-side FieldOrder — the key of the record values.</summary>
        public async Task<List<MauiApp1.Models.FieldDefinition>> GetFieldDefinitionsAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"category/{categoryId}/fields", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ошибка при получении полей категории {CategoryId}: {StatusCode}", categoryId, response.StatusCode);
                    return [];
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return await response.Content.ReadFromJsonAsync<List<MauiApp1.Models.FieldDefinition>>(options, cancellationToken) ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                _logger.LogError(ex, "Ошибка при получении полей категории {CategoryId}", categoryId);
                return [];
            }
        }

        public async Task<bool> SaveCategoryFieldsAsync(int categoryId, List<string> fieldNames, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"category/{Uri.EscapeDataString(categoryId.ToString())}/addOrChangeFields";
                var response = await _httpClient.PostAsJsonAsync(url, fieldNames, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при сохранении полей категории {categoryId}: {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при сохранении полей категории {categoryId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при сохранении полей категории {categoryId}");
                throw;
            }
        }
    }
}