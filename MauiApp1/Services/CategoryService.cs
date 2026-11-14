using MauiApp1.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace MauiApp1.Services
{
    class CategoryService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILogger<CategoryService> _logger;

        // The JWT is attached by AuthHeaderHandler; this service used to mutate DefaultRequestHeaders of the shared client.
        public CategoryService(HttpClient httpClient, ILogger<CategoryService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> AddCategoryAsync(string categoryName)
        {
            try
            {
                var request = new CategoryModel
                {
                    Name = categoryName.Trim(),
                    Type = "Custom"
                };

                var response = await _httpClient.PostAsJsonAsync("category/createCategory", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка создания категории");
                return false;
            }
        }

        public async Task<List<CategoryModel>> GetCategoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("category/getAllCategories");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<CategoryModel>>(_jsonOptions)
                        ?? new List<CategoryModel>();
                }
                return new List<CategoryModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения категорий");
                return new List<CategoryModel>();
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"category/{categoryId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления категории");
                return false;
            }
        }

        public async Task<string> GetCurrentNameOfCategory(int categoryId)
        {
            var categories = await GetCategoriesAsync();
            var currentCategory = categories.FirstOrDefault(c => c.Id == categoryId);
            return currentCategory?.Name ?? "Неизвестная категория";
        }
    }
}
