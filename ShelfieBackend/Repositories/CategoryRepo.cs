using Microsoft.EntityFrameworkCore;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using System.Security.Claims;
using ShelfieBackend.States;
using static ShelfieBackend.Responses.CustomResponses;
using Serilog;
using ShelfieBackend.Services;

namespace ShelfieBackend.Repositories
{
    public class CategoryRepo : ICategoryRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IUserIdService _userService;
        public CategoryRepo(ApplicationDbContext appDbContext, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _userService = userService;
        }

        public async Task<BaseResponse> CreateCategoryAsync(string categoryName, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);

                if (string.IsNullOrWhiteSpace(categoryName))
                    throw new ArgumentException("Название категории не может быть пустым", nameof(categoryName));

                categoryName = categoryName.Trim();

                // Ищем категорию по названию и UserId
                var category = await _appDbContext.Categories
                    .FirstOrDefaultAsync(c => c.Name == categoryName && c.UserId == userId, cancellationToken);
                if (category != null)
                    return new BaseResponse(false, "Категория уже существует");

                var newCategory = new Category
                {
                    Name = categoryName,
                    Type = Constants.CustomType,
                    UserId = userId
                };

                _appDbContext.Categories.Add(newCategory);
                await _appDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse(true, "Категория успешно создана");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while creating a category");
                return new BaseResponse(false, "Error while creating a category");
            }
        }

        public async Task<List<GetCategoriesDTO>> GetAllCategories(ClaimsPrincipal сurrentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(сurrentUser);
                if (userId == null)
                    return new List<GetCategoriesDTO>();

                return await _appDbContext.Categories
                    .Where(p => p.UserId == userId.Value)
                    .AsNoTracking()
                    .Select(h => new GetCategoriesDTO
                    {
                        Id = h.Id,
                        Name = h.Name,
                        Type = h.Type,
                        UserId = h.UserId
                    })
                .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"An error occurred while retrieving categories");
                throw new Exception("Error while retrieving categories");
            }
        }

        public async Task<BaseResponse> DeleteCategoryAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден");

                var category = await _appDbContext.Categories
                    .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

                if (category == null)
                    return new BaseResponse(false, "Категория не найдена");

                _appDbContext.Categories.Remove(category);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return new BaseResponse(true, "Категория успешно удалена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при удалении категории");
                return new BaseResponse(false, "Ошибка при удалении категории");
            }
        }
    }
}