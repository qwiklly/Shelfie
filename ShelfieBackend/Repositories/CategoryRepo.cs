using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using ShelfieBackend.States;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class CategoryRepo : ICategoryRepo
    {
        public const int MaxNameLength = 100;

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
                // Without a user the category would become a "predefined" one (UserId == null) visible to everybody.
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден");

                categoryName = categoryName?.Trim() ?? string.Empty;
                if (categoryName.Length == 0)
                    return new BaseResponse(false, "Название категории не может быть пустым");
                if (categoryName.Length > MaxNameLength)
                    return new BaseResponse(false, $"Название категории не может быть длиннее {MaxNameLength} символов");

                bool exists = await _appDbContext.Categories
                    .AnyAsync(c => c.Name == categoryName && c.UserId == userId, cancellationToken);
                if (exists)
                    return new BaseResponse(false, "Категория уже существует");

                _appDbContext.Categories.Add(new Category
                {
                    Name = categoryName,
                    Type = Constants.CustomType,
                    UserId = userId
                });
                await _appDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse(true, "Категория успешно создана");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while creating a category");
                return new BaseResponse(false, "Error while creating a category");
            }
        }

        public async Task<List<GetCategoriesDTO>> GetAllCategories(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<GetCategoriesDTO>();

            return await _appDbContext.Categories
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .OrderBy(c => c.Id)
                .Select(h => new GetCategoriesDTO
                {
                    Id = h.Id,
                    Name = h.Name,
                    Type = h.Type,
                    UserId = h.UserId
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<GetCategoriesDTO?> GetCategoryAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return null;

            return await _appDbContext.Categories
                .AsNoTracking()
                .Where(p => p.Id == categoryId && p.UserId == userId.Value)
                .Select(h => new GetCategoriesDTO
                {
                    Id = h.Id,
                    Name = h.Name,
                    Type = h.Type,
                    UserId = h.UserId
                })
                .FirstOrDefaultAsync(cancellationToken);
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

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                // CategoryFieldValues has no foreign key to Categories, so its rows are not cascaded.
                await _appDbContext.CategoryFieldValues
                    .Where(v => v.CategoryId == categoryId)
                    .ExecuteDeleteAsync(cancellationToken);

                _appDbContext.Categories.Remove(category);
                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

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
