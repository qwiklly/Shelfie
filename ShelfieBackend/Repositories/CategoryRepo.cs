using Microsoft.EntityFrameworkCore;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class CategoryRepo : ICategoryRepo
    {
        private readonly ApplicationDbContext _appDbContext;

        public CategoryRepo(ApplicationDbContext appDbContex)
        {
            _appDbContext = appDbContex;
        }

        public async Task<BaseResponse> CreateCategoryAsync(string categoryName, ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);

                if (string.IsNullOrWhiteSpace(categoryName))
                    throw new ArgumentException("Название категории не может быть пустым", nameof(categoryName));

                categoryName = categoryName.Trim();

                // Ищем категорию по названию и UserId
                var category = await _appDbContext.Categories
                    .FirstOrDefaultAsync(c => c.Name == categoryName && c.UserId == userId);
                if (category != null)
                    return new BaseResponse(false, "Категория уже существует");

                // Создаем новую категорию как "Custom"
                var newCategory = new Category
                {
                    Name = categoryName,
                    Type = "Custom", // Всегда "Custom", потому что предопределенные уже есть
                    UserId = userId
                };

                _appDbContext.Categories.Add(newCategory);
                await _appDbContext.SaveChangesAsync();
                return new BaseResponse(true, "Категория успешно создана");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred while creating a category: {ex.Message}");
                return new BaseResponse(false, "Error while creating a category");
            }
        }
        public async Task<List<GetCategoriesDTO>> GetAllCategories(ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new List<GetCategoriesDTO>();

                return await _appDbContext.Categories
                    .Where(p => p.UserId == userId.Value)
                     .Select(h => new GetCategoriesDTO
                     {
                         Id = h.Id,
                         Name = h.Name,
                         Type = h.Type,
                         UserId = h.UserId
                     })
                .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"An error occurred while retrieving categories: {ex.Message}");
                throw new Exception("Error while retrieving categories");
            }
        }
        protected internal int? GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || userId == 0)
                return null;
            return userId;
        }
    }
}