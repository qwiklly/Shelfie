using MauiApp1.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MauiApp1.Services
{
    class CategoryService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILogger<CategoryService> _logger;
        public CategoryService(HttpClient httpClient, AuthService authService, ILogger<CategoryService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _logger = logger;
        }

        public async Task<bool> AddCategoryAsync(string categoryName)
        {
            try
            {
                await SetAuthorizationHeaderAsync();

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
                _logger.LogError($"Ошибка удаления аккаунта: {ex.Message}");
                return false;
            }
        }
        public async Task<List<CategoryModel>> GetCategoriesAsync()
        {
            try
            {
                await SetAuthorizationHeaderAsync();

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
                _logger.LogError($"Ошибка удаления аккаунта: {ex.Message}");
                throw;
            }
        }
        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                await SetAuthorizationHeaderAsync();
                var response = await _httpClient.DeleteAsync($"category/{categoryId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка удаления аккаунта: {ex.Message}");
                return false;
            }
        }

        private async Task SetAuthorizationHeaderAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
        public async Task<string> GetCurrentNameOfCategory(int categoryId)
        {
            try
            {
                await SetAuthorizationHeaderAsync();

                var categories = await GetCategoriesAsync();
                var currentCategory = categories.FirstOrDefault(c => c.Id == categoryId);
                return currentCategory?.Name ?? "Неизвестная категория";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка получения названия текущей категории: {ex.Message}");
                throw;
            }
        }
    }
}