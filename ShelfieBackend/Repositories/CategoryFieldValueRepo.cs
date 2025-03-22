using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class CategoryFieldValueRepo : ICategoryFieldValueRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IUserIdService _userService;
        private readonly IHistoryRepo _historyRepository;

        public CategoryFieldValueRepo(ApplicationDbContext appDbContext, IUserIdService userService, IHistoryRepo historyRepository)
        {
            _appDbContext = appDbContext;
            _userService = userService;
            _historyRepository = historyRepository;
        }

        public async Task<BaseResponse> PostFieldValuesAsync(int categoryId, FieldValuesRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Ошибка аутентификации");

                var category = await _appDbContext.Categories
                    .Where(c => c.Id == categoryId && c.UserId == userId)
                    .Select(c => new { c.Id, c.Name })
                    .FirstOrDefaultAsync(cancellationToken);
                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                bool recordExists = await _appDbContext.CategoryFieldValues
                   .AnyAsync(v => v.CategoryId == categoryId && v.RecordId == request.RecordId, cancellationToken);
                if (recordExists)
                    return new BaseResponse(false, "Запись с таким RecordId уже существует");

                var validFieldIds = await _appDbContext.CategoryFields
                    .Where(f => f.CategoryId == categoryId)
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken);

                var newValues = request.FieldValues
                    .Where(fv => validFieldIds.Contains(fv.Key))
                    .Select(fv => new CategoryFieldValue
                    {
                        CategoryId = categoryId,
                        RecordId = request.RecordId,
                        CategoryFieldId = fv.Key,
                        Value = fv.Value.Trim()
                    }).ToList();

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                await _appDbContext.CategoryFieldValues.AddRangeAsync(newValues, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Custom",
                    ItemName = category.Name,
                    ItemId = request.RecordId,
                    CategoryId = categoryId,
                    ChangeType = "Added",
                    UserId = userId.Value,
                }, cancellationToken);


                await transaction.CommitAsync(cancellationToken);
                return new BaseResponse(true, "Данные успешно добавлены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при добавлении значений");
                return new BaseResponse(false, "Ошибка при добавлении значений");
            }
        }

        public async Task<BaseResponse> UpdateFieldValuesAsync(int categoryId, FieldValuesRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Ошибка аутентификации");

                var category = await _appDbContext.Categories
                    .Where(c => c.Id == categoryId && c.UserId == userId)
                    .Select(c => new { c.Id, c.Name })
                    .FirstOrDefaultAsync(cancellationToken);

                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var existingValues = await _appDbContext.CategoryFieldValues
                    .Where(v => v.CategoryId == categoryId && v.RecordId == request.RecordId)
                    .ToListAsync(cancellationToken);

                if (!existingValues.Any())
                    return new BaseResponse(false, "Запись с таким RecordId не найдена");

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                // Обновляем значения
                foreach (var value in existingValues)
                {
                    // Используем CategoryFieldId для сопоставления
                    if (request.FieldValues.TryGetValue(value.CategoryFieldId, out var newValue))
                    {
                        value.Value = newValue;
                    }
                }

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Custom",
                    ItemName = category.Name,
                    ItemId = request.RecordId,
                    CategoryId = categoryId,
                    ChangeType = "Updated",
                    UserId = userId.Value
                }, cancellationToken);

                // Сохраняем изменения
                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Данные успешно обновлены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обновлении значений");
                return new BaseResponse(false, "Ошибка при обновлении значений");
            }
        }

        public async Task<List<FieldValuesRequest>> GetFieldValuesAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<FieldValuesRequest>();

                var category = await _appDbContext.Categories
                    .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
                if (category == null)
                    return new List<FieldValuesRequest>();

                var values = await _appDbContext.CategoryFieldValues
                    .Where(v => v.CategoryId == categoryId)
                    .OrderBy(v => v.RecordId)
                    .ToListAsync(cancellationToken);

                if (!values.Any())
                {
                    Log.Warning($"Нет значений для категории {categoryId}");
                    return new List<FieldValuesRequest>();
                }

                return values
                    .GroupBy(v => v.RecordId)
                    .Select(g => new FieldValuesRequest
                    {
                        RecordId = g.Key,
                        FieldValues = g.ToDictionary(v => v.CategoryFieldId, v => v.Value)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении значений полей");
                return new List<FieldValuesRequest>();
            }
        }

        public async Task<BaseResponse> DeleteFieldValuesAsync(int categoryId, int recordId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Ошибка аутентификации");

                var category = await _appDbContext.Categories
                  .Where(c => c.Id == categoryId && c.UserId == userId)
                  .Select(c => new { c.Id, c.Name })
                  .FirstOrDefaultAsync(cancellationToken);
                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var valuesToDelete = await _appDbContext.CategoryFieldValues
                 .Where(v => v.CategoryId == categoryId && v.RecordId == recordId)
                 .ToListAsync(cancellationToken);

                if (!valuesToDelete.Any())
                    return new BaseResponse(false, "Значения не найдены");

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                _appDbContext.CategoryFieldValues.RemoveRange(valuesToDelete);
                await _appDbContext.SaveChangesAsync(cancellationToken);


                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Custom",
                    ItemName = category.Name,
                    ItemId = recordId,
                    CategoryId = categoryId,
                    ChangeType = "Deleted",
                    UserId = userId.Value
                }, cancellationToken);


                await transaction.CommitAsync(cancellationToken);
                return new BaseResponse(true, "Запись удалена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при удалении записи");
                return new BaseResponse(false, "Ошибка при удалении записи");
            }
        }
    }
}
