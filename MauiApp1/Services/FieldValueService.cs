using System.Net.Http.Json;
using System.Text.Json;
using MauiApp1.Models;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Services
{
    public class FieldValueService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FieldValueService> _logger;

        public FieldValueService(HttpClient httpClient, ILogger<FieldValueService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<FieldValuesRequest>> GetFieldValuesForCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"categoryValue/{Uri.EscapeDataString(categoryId.ToString())}/values";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при получении значений для категории {categoryId}: {response.StatusCode}");
                    return [];
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = await response.Content.ReadFromJsonAsync<List<FieldValuesRequest>>(options, cancellationToken);
                return result ?? [];
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при получении значений для категории {categoryId}");
                return [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Ошибка парсинга JSON при получении значений для категории {categoryId}");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при получении значений для категории {categoryId}");
                throw;
            }
        }

        public async Task<bool> SaveFieldValuesForCategoryAsync(int categoryId, FieldValuesRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"categoryValue/{Uri.EscapeDataString(categoryId.ToString())}/save-values";
                var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при сохранении значений для категории {categoryId}: {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при сохранении значений для категории {categoryId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при сохранении значений для категории {categoryId}");
                throw;
            }
        }

        public async Task<bool> UpdateFieldValuesForCategoryAsync(int categoryId, FieldValuesRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"categoryValue/{Uri.EscapeDataString(categoryId.ToString())}/update-values";
                var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при обновлении значений для категории {categoryId}: {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при обновлении значений для категории {categoryId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при обновлении значений для категории {categoryId}");
                throw;
            }
        }

        public async Task<bool> DeleteFieldValuesAsync(int categoryId, int recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"categoryValue/{Uri.EscapeDataString(categoryId.ToString())}/{Uri.EscapeDataString(recordId.ToString())}";
                var response = await _httpClient.DeleteAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Ошибка при удалении записи {recordId} в категории {categoryId}: {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Сетевое исключение при удалении записи {recordId} в категории {categoryId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неизвестная ошибка при удалении записи {recordId} в категории {categoryId}");
                throw;
            }
        }
    }
}
