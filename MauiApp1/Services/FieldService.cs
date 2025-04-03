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