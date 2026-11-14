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
    public class CategoryFieldRepo : ICategoryFieldRepo
    {
        public const int MaxFields = 20;

        private readonly ApplicationDbContext _appDbContext;
        private readonly IUserIdService _userService;

        public CategoryFieldRepo(ApplicationDbContext appDbContext, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _userService = userService;
        }

        /// <summary>
        /// Replaces the set of fields of a custom category. Fields are matched by name: existing ones keep their
        /// FieldOrder (and therefore their values), missing ones are removed together with their values, and new
        /// ones get a fresh FieldOrder plus an empty value in every existing record.
        /// </summary>
        public async Task<BaseResponse> SaveCategoryFieldsAsync(int categoryId, List<string> fieldNames, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден");

                fieldNames = (fieldNames ?? new List<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (fieldNames.Count == 0)
                    return new BaseResponse(false, "Необходимо передать хотя бы одно поле");
                if (fieldNames.Count > MaxFields)
                    return new BaseResponse(false, $"У категории может быть не больше {MaxFields} полей");

                bool ownsCategory = await _appDbContext.Categories
                    .AnyAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
                if (!ownsCategory)
                    return new BaseResponse(false, "Категория не найдена или не принадлежит пользователю");

                var existingFields = await _appDbContext.CategoryFields
                    .Where(f => f.CategoryId == categoryId)
                    .ToListAsync(cancellationToken);

                var existingNames = existingFields.Select(f => f.FieldName).ToHashSet(StringComparer.Ordinal);
                int maxOrder = existingFields.Count > 0 ? existingFields.Max(f => f.FieldOrder) : 0;

                var fieldsToDelete = existingFields.Where(f => !fieldNames.Contains(f.FieldName)).ToList();
                var newFields = fieldNames
                    .Where(name => !existingNames.Contains(name))
                    .Select(name => new CategoryField
                    {
                        CategoryId = categoryId,
                        FieldName = name,
                        FieldOrder = ++maxOrder
                    })
                    .ToList();

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                if (fieldsToDelete.Count > 0)
                {
                    var ordersToDelete = fieldsToDelete.Select(f => f.FieldOrder).ToList();
                    await _appDbContext.CategoryFieldValues
                        .Where(v => v.CategoryId == categoryId && ordersToDelete.Contains(v.FieldOrder))
                        .ExecuteDeleteAsync(cancellationToken);

                    _appDbContext.CategoryFields.RemoveRange(fieldsToDelete);
                }

                if (newFields.Count > 0)
                {
                    await _appDbContext.CategoryFields.AddRangeAsync(newFields, cancellationToken);

                    var existingRecordIds = await _appDbContext.CategoryFieldValues
                        .Where(v => v.CategoryId == categoryId)
                        .Select(v => v.RecordId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    // Every existing record gets an empty value for every new field.
                    var emptyValues = existingRecordIds
                        .SelectMany(recordId => newFields.Select(field => new CategoryFieldValue
                        {
                            CategoryId = categoryId,
                            RecordId = recordId,
                            FieldOrder = field.FieldOrder,
                            Value = string.Empty
                        }))
                        .ToList();

                    await _appDbContext.CategoryFieldValues.AddRangeAsync(emptyValues, cancellationToken);
                }

                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
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
            var definitions = await GetCategoryFieldDefinitionsAsync(categoryId, currentUser, cancellationToken);
            return definitions.Select(d => d.Name).ToList();
        }

        public async Task<List<CategoryFieldDTO>> GetCategoryFieldDefinitionsAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<CategoryFieldDTO>();

            bool ownsCategory = await _appDbContext.Categories
                .AnyAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
            if (!ownsCategory)
                return new List<CategoryFieldDTO>();

            return await _appDbContext.CategoryFields
                .AsNoTracking()
                .Where(f => f.CategoryId == categoryId)
                .OrderBy(f => f.FieldOrder)
                .Select(f => new CategoryFieldDTO { FieldOrder = f.FieldOrder, Name = f.FieldName })
                .ToListAsync(cancellationToken);
        }
    }
}
