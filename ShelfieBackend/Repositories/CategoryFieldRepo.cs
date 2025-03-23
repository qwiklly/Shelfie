using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class CategoryFieldRepo : ICategoryFieldRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IUserIdService _userService;

        public CategoryFieldRepo(ApplicationDbContext appDbContext, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _userService = userService;
        }
        public async Task<BaseResponse> SaveCategoryFieldsAsync(int categoryId, List<string> fieldNames, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (!fieldNames.Any())
                    return new BaseResponse(false, "Необходимо передать хотя бы одно поле");

                fieldNames = fieldNames.Select(name => name.Trim()).Distinct().ToList();

                var category = await _appDbContext.Categories
                    .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var existingFields = await _appDbContext.CategoryFields
                    .Where(f => f.CategoryId == categoryId)
                    .ToListAsync(cancellationToken);

                var existingFieldMap = existingFields.ToDictionary(f => f.FieldName, f => f);

                int maxOrder = existingFields.Any() ? existingFields.Max(f => f.FieldOrder) : 0;

                // Определяем, какие поля удалить
                var fieldsToDelete = existingFields.Where(f => !fieldNames.Contains(f.FieldName)).ToList();

                // Определяем новые поля и присваиваем им порядковый номер
                var newFields = fieldNames
                    .Where(name => !existingFieldMap.ContainsKey(name))
                    .Select(name => new CategoryField
                    {
                        CategoryId = categoryId,
                        FieldName = name,
                        FieldOrder = ++maxOrder // Увеличиваем порядок на 1
                    })
                    .ToList();

                // Удаление значений, связанных с удаляемыми полями
                if (fieldsToDelete.Any())
                {
                    var fieldOrdersToDelete = fieldsToDelete.Select(f => f.FieldOrder).ToList();
                    var valuesToDelete = _appDbContext.CategoryFieldValues
                        .Where(v => fieldOrdersToDelete.Contains(v.FieldOrder) && v.CategoryId == categoryId);

                    _appDbContext.CategoryFieldValues.RemoveRange(valuesToDelete);
                    _appDbContext.CategoryFields.RemoveRange(fieldsToDelete);
                }

                // Добавляем новые поля
                if (newFields.Any())
                    await _appDbContext.CategoryFields.AddRangeAsync(newFields, cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse(true, "Поля успешно сохранены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при сохранении полей категории");
                return new BaseResponse(false, "Ошибка при сохранении полей категории");
            }
        }

        public async Task<List<string>> GetCategoryFieldsAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<string>();

                var categoryExists = await _appDbContext.Categories
                    .AnyAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

                if (!categoryExists)
                    return new List<string>();

                return await _appDbContext.CategoryFields
                    .Where(f => f.CategoryId == categoryId)
                    .AsNoTracking()
                    .Select(f => f.FieldName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении полей категории");
                return new List<string>();
            }
        }
    }
}
