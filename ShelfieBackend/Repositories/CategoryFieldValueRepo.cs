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

        /// <summary>
        /// Adds a record. When <see cref="FieldValuesRequest.RecordId"/> is 0 or negative the server assigns the next free id.
        /// Values are stored for every field of the category (missing ones as empty strings).
        /// </summary>
        public async Task<BaseResponse> PostFieldValuesAsync(int categoryId, FieldValuesRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Ошибка аутентификации");

                var category = await FindOwnedCategoryAsync(categoryId, userId.Value, cancellationToken);
                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var fieldOrders = await GetFieldOrdersAsync(categoryId, cancellationToken);
                if (fieldOrders.Count == 0)
                    return new BaseResponse(false, "Сначала задайте поля категории");

                var values = request.FieldValues ?? new Dictionary<int, string>();
                if (!values.Any(v => fieldOrders.Contains(v.Key) && !string.IsNullOrWhiteSpace(v.Value)))
                    return new BaseResponse(false, "Заполните хотя бы одно поле");

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                int recordId = request.RecordId;
                if (recordId <= 0)
                {
                    recordId = (await _appDbContext.CategoryFieldValues
                        .Where(v => v.CategoryId == categoryId)
                        .MaxAsync(v => (int?)v.RecordId, cancellationToken) ?? 0) + 1;
                }
                else if (await _appDbContext.CategoryFieldValues
                    .AnyAsync(v => v.CategoryId == categoryId && v.RecordId == recordId, cancellationToken))
                {
                    return new BaseResponse(false, "Запись с таким RecordId уже существует");
                }

                var newValues = fieldOrders.Select(order => new CategoryFieldValue
                {
                    CategoryId = categoryId,
                    RecordId = recordId,
                    FieldOrder = order,
                    Value = values.TryGetValue(order, out var value) ? (value ?? string.Empty).Trim() : string.Empty
                }).ToList();

                await _appDbContext.CategoryFieldValues.AddRangeAsync(newValues, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeCustom,
                    ItemName = DescribeRecord(category.Name, newValues),
                    ItemId = recordId,
                    CategoryId = categoryId,
                    ChangeType = Constants.ChangeAdded,
                    QuantityChange = 1,
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

                var category = await FindOwnedCategoryAsync(categoryId, userId.Value, cancellationToken);
                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var existingValues = await _appDbContext.CategoryFieldValues
                    .Where(v => v.CategoryId == categoryId && v.RecordId == request.RecordId)
                    .ToListAsync(cancellationToken);

                if (existingValues.Count == 0)
                    return new BaseResponse(false, "Запись с таким RecordId не найдена");

                var fieldOrders = await GetFieldOrdersAsync(categoryId, cancellationToken);
                var values = request.FieldValues ?? new Dictionary<int, string>();

                foreach (var (order, rawValue) in values)
                {
                    if (!fieldOrders.Contains(order))
                        continue;

                    var newValue = (rawValue ?? string.Empty).Trim();
                    var existing = existingValues.FirstOrDefault(v => v.FieldOrder == order);
                    if (existing != null)
                    {
                        existing.Value = newValue;
                    }
                    else
                    {
                        // Field added after the record was created and no placeholder exists yet.
                        var added = new CategoryFieldValue
                        {
                            CategoryId = categoryId,
                            RecordId = request.RecordId,
                            FieldOrder = order,
                            Value = newValue
                        };
                        existingValues.Add(added);
                        _appDbContext.CategoryFieldValues.Add(added);
                    }
                }

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeCustom,
                    ItemName = DescribeRecord(category.Name, existingValues),
                    ItemId = request.RecordId,
                    CategoryId = categoryId,
                    ChangeType = Constants.ChangeUpdated,
                    UserId = userId.Value
                }, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Данные успешно обновлены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обновлении значений");
                return new BaseResponse(false, "Ошибка при обновлении значений");
            }
        }

        /// <summary>
        /// Returns all records of a category. Every record contains a value (possibly empty) for every current field,
        /// so clients can index by FieldOrder safely.
        /// </summary>
        public async Task<List<FieldValuesRequest>> GetFieldValuesAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<FieldValuesRequest>();

            var category = await FindOwnedCategoryAsync(categoryId, userId.Value, cancellationToken);
            if (category == null)
                return new List<FieldValuesRequest>();

            var fieldOrders = await GetFieldOrdersAsync(categoryId, cancellationToken);

            var values = await _appDbContext.CategoryFieldValues
                .AsNoTracking()
                .Where(v => v.CategoryId == categoryId)
                .OrderBy(v => v.RecordId)
                .ThenBy(v => v.Id)
                .ToListAsync(cancellationToken);

            return values
                .GroupBy(v => v.RecordId)
                .Select(g =>
                {
                    var byOrder = new Dictionary<int, string>();
                    foreach (var v in g.Where(v => fieldOrders.Contains(v.FieldOrder)))
                        byOrder[v.FieldOrder] = v.Value; // tolerate duplicates left by older versions

                    return new FieldValuesRequest
                    {
                        RecordId = g.Key,
                        FieldValues = fieldOrders.ToDictionary(o => o, o => byOrder.GetValueOrDefault(o, string.Empty))
                    };
                })
                .ToList();
        }

        public async Task<BaseResponse> DeleteFieldValuesAsync(int categoryId, int recordId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Ошибка аутентификации");

                var category = await FindOwnedCategoryAsync(categoryId, userId.Value, cancellationToken);
                if (category == null)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var valuesToDelete = await _appDbContext.CategoryFieldValues
                    .Where(v => v.CategoryId == categoryId && v.RecordId == recordId)
                    .ToListAsync(cancellationToken);

                if (valuesToDelete.Count == 0)
                    return new BaseResponse(false, "Значения не найдены");

                var description = DescribeRecord(category.Name, valuesToDelete);

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                _appDbContext.CategoryFieldValues.RemoveRange(valuesToDelete);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeCustom,
                    ItemName = description,
                    ItemId = recordId,
                    CategoryId = categoryId,
                    ChangeType = Constants.ChangeRemoved,
                    QuantityChange = -1,
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

        private Task<Category?> FindOwnedCategoryAsync(int categoryId, int userId, CancellationToken cancellationToken) =>
            _appDbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        private Task<List<int>> GetFieldOrdersAsync(int categoryId, CancellationToken cancellationToken) =>
            _appDbContext.CategoryFields
                .Where(f => f.CategoryId == categoryId)
                .OrderBy(f => f.FieldOrder)
                .Select(f => f.FieldOrder)
                .ToListAsync(cancellationToken);

        /// <summary>"Books: War and Peace" — category name plus the first non-empty value of the record.</summary>
        internal static string DescribeRecord(string categoryName, IEnumerable<CategoryFieldValue> values)
        {
            var first = values
                .OrderBy(v => v.FieldOrder)
                .Select(v => v.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            return string.IsNullOrEmpty(first) ? categoryName : $"{categoryName}: {first}";
        }
    }
}
