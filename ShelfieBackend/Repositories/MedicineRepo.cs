using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using ShelfieBackend.States;
using System.Linq.Expressions;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class MedicineRepo : IMedicineRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IHistoryRepo _historyRepository;
        private readonly IUserIdService _userService;

        public MedicineRepo(ApplicationDbContext appDbContext, IHistoryRepo historyRepository, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _historyRepository = historyRepository;
            _userService = userService;
        }

        public async Task<BaseResponse> AddMedicamentAsync(AddProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден.");

                var name = model.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                    return new BaseResponse(false, "Название лекарства не может быть пустым.");

                // Medicaments are addressed by name in the API, so names must be unique per user.
                bool exists = await _appDbContext.Medications
                    .AnyAsync(p => p.UserId == userId.Value && p.Name == name, cancellationToken);
                if (exists)
                    return new BaseResponse(false, "Лекарство с таким названием уже существует.");

                var categoryId = await CategoryResolver.ResolveAsync(_appDbContext, model.CategoryId, userId.Value, Constants.MedicationsCategoryId, cancellationToken);

                var medicament = new Medication
                {
                    Name = name,
                    Creator = model.Creator?.Trim() ?? string.Empty,
                    Description = model.Description,
                    CategoryId = categoryId,
                    ExpirationDate = model.ExpirationDate,
                    Quantity = model.Quantity,
                    UserId = userId.Value,
                    CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    Weight = model.Weight,
                    WeightUnit = model.WeightUnit ?? string.Empty,
                };

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                await _appDbContext.Medications.AddAsync(medicament, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeMedicament,
                    ItemName = medicament.Name,
                    ItemId = medicament.Id,
                    CategoryId = medicament.CategoryId,
                    ChangeType = Constants.ChangeAdded,
                    QuantityChange = medicament.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Лекарство успешно добавлено.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while adding a medication");
                return new BaseResponse(false, "Error while adding a medication");
            }
        }

        public async Task<List<GetProductDTO>> GetMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<GetProductDTO>();

            return await _appDbContext.Medications
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .OrderBy(p => p.Name)
                .Select(ToDto)
                .ToListAsync(cancellationToken);
        }

        public async Task<GetProductDTO?> GetMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return null;

            return await _appDbContext.Medications
                .AsNoTracking()
                .Where(p => p.Name == name && p.UserId == userId.Value)
                .Select(ToDto)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<BaseResponse> DeleteMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден.");

                var medicament = await _appDbContext.Medications
                    .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
                if (medicament == null)
                    return new BaseResponse(false, "Лекарство не найдено.");

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeMedicament,
                    ItemName = medicament.Name,
                    ItemId = medicament.Id,
                    CategoryId = medicament.CategoryId,
                    ChangeType = Constants.ChangeRemoved,
                    QuantityChange = -medicament.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                _appDbContext.Medications.Remove(medicament);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Лекарство успешно удалено.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while deleting a medicament");
                return new BaseResponse(false, "Error while deleting a medicament");
            }
        }

        public async Task<BaseResponse> UpdateMedicamentAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден.");

                var medicament = await _appDbContext.Medications
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value, cancellationToken);
                if (medicament == null)
                    return new BaseResponse(false, "Лекарство не найдено.");

                if (model.Quantity < 0 || model.Weight < 0)
                    return new BaseResponse(false, "Количество и вес не могут быть отрицательными.");

                var newName = string.IsNullOrWhiteSpace(model.Name) ? medicament.Name : model.Name.Trim();
                if (newName != medicament.Name &&
                    await _appDbContext.Medications.AnyAsync(p => p.UserId == userId.Value && p.Name == newName, cancellationToken))
                {
                    return new BaseResponse(false, "Лекарство с таким названием уже существует.");
                }

                int oldQuantity = medicament.Quantity;

                medicament.Name = newName;
                medicament.Creator = model.Creator?.Trim() ?? medicament.Creator;
                medicament.Description = model.Description ?? medicament.Description;
                medicament.ExpirationDate = model.ExpirationDate ?? medicament.ExpirationDate;
                medicament.Quantity = model.Quantity ?? medicament.Quantity;
                medicament.Weight = model.Weight is null or 0 ? medicament.Weight : model.Weight;
                medicament.WeightUnit = string.IsNullOrWhiteSpace(model.WeightUnit) ? medicament.WeightUnit : model.WeightUnit;

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeMedicament,
                    ItemName = medicament.Name,
                    ItemId = medicament.Id,
                    CategoryId = medicament.CategoryId,
                    ChangeType = Constants.ChangeUpdated,
                    QuantityChange = medicament.Quantity - oldQuantity,
                    UserId = userId.Value
                }, cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Лекарство успешно обновлено.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a medicament");
                return new BaseResponse(false, "Error while updating a medicament");
            }
        }

        public async Task<List<GetProductDTO>> GetExpiredMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<GetProductDTO>();

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await _appDbContext.Medications
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value && p.ExpirationDate != null && p.ExpirationDate < today)
                .OrderBy(p => p.ExpirationDate)
                .Select(ToDto)
                .ToListAsync(cancellationToken);
        }

        private static readonly Expression<Func<Medication, GetProductDTO>> ToDto = p => new GetProductDTO
        {
            Id = p.Id,
            Name = p.Name,
            Creator = p.Creator,
            CategoryId = p.CategoryId,
            ExpirationDate = p.ExpirationDate,
            Quantity = p.Quantity,
            CreatedAt = p.CreatedAt,
            Description = p.Description,
            Weight = p.Weight ?? 0,
            WeightUnit = p.WeightUnit,
        };
    }
}
