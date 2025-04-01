using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using static ShelfieBackend.Responses.CustomResponses;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ShelfieBackend.Repositories
{
    public class MedicineRepo : IMedicineRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IHistoryRepo _historyRepository;
        private readonly IUserIdService _userService;

        public MedicineRepo(ApplicationDbContext appDbContext, IConfiguration config, IHistoryRepo historyRepository, IUserIdService userService)
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
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var category = await _appDbContext.Categories.FindAsync(model.CategoryId, cancellationToken);
                var categoryId = category != null ? model.CategoryId : 1;

                var medicament = new Medication
                {
                    Name = model.Name,
                    Creator = model.Creator,
                    CategoryId = categoryId,
                    ExpirationDate = model.ExpirationDate,
                    Quantity = model.Quantity,
                    UserId = userId.Value,
                    CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    Weight = model.Weight,
                    WeightUnit = model.WeightUnit,
                };

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                await _appDbContext.Medications.AddAsync(medicament, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Medicament",
                    ItemName = medicament.Name,
                    ItemId = medicament.Id,
                    CategoryId = medicament.CategoryId,
                    ChangeType = "Added",
                    QuantityChange = medicament.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Лекарство успешно добавлено." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while adding a medication");
                return new BaseResponse(false, "Error while adding a medication");
            }
        }

        public async Task<List<GetProductDTO>> GetMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<GetProductDTO>();

                return await _appDbContext.Medications
                    .Where(p => p.UserId == userId.Value)
                    .Select(p => new GetProductDTO
                    {
                        Name = p.Name,
                        Creator = p.Creator,
                        CategoryId = p.CategoryId,
                        ExpirationDate = p.ExpirationDate,
                        Quantity = p.Quantity,
                        CreatedAt = p.CreatedAt,
                        Weight = p.Weight ?? 0,
                        WeightUnit = p.WeightUnit,
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting medications");
                throw new Exception("Error while getting medications");
            }
        }

        public async Task<Medication?> GetMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return null;

            return await _appDbContext.Medications
                .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
        }

        public async Task<BaseResponse> DeleteMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var medicament = await _appDbContext.Medications
                    .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
                if (medicament == null)
                    return new BaseResponse { Flag = false, Message = "Лекарство не найден." };

                // Сохранить количество для истории
                int quantity = medicament.Quantity;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = medicament.Creator,
                    ItemId = medicament.Id,
                    ChangeType = "Removed",
                    QuantityChange = -quantity,
                    UserId = userId.Value
                }, cancellationToken);

                _appDbContext.Medications.Remove(medicament);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Лекарство успешно удалено." };
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
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var medicament = await _appDbContext.Medications
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value, cancellationToken);
                if (medicament == null)
                    return new BaseResponse { Flag = false, Message = "Лекарство не найдено." };

                int oldQuantity = medicament.Quantity;

                medicament.Name = model.Name ?? medicament.Name;
                medicament.Creator = model.Creator ?? medicament.Creator;
                medicament.ExpirationDate = model.ExpirationDate ?? medicament.ExpirationDate;
                medicament.Quantity = model.Quantity ?? medicament.Quantity;
                medicament.Weight = model.Weight == 0 ? medicament.Weight : model.Weight;
                medicament.WeightUnit = model.WeightUnit ?? medicament.WeightUnit;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                int quantityChange = medicament.Quantity - oldQuantity;

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = medicament.Name,
                    ItemId = medicament.Id,
                    ChangeType = "Updated",
                    QuantityChange = quantityChange,
                    UserId = userId.Value
                }, cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Лекарство успешно обновлено." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a product");
                return new BaseResponse(false, "Error while updating a product");
            }
        }
        public async Task<List<GetProductDTO>> GetExpiredMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<GetProductDTO>();

                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

                return await _appDbContext.Medications
                    .Where(p => p.UserId == userId.Value && p.ExpirationDate != null && p.ExpirationDate < today)
                    .Select(p => new GetProductDTO
                    {
                        Name = p.Name,
                        Creator = p.Creator,
                        CategoryId = p.CategoryId,
                        ExpirationDate = p.ExpirationDate,
                        Quantity = p.Quantity,
                        CreatedAt = p.CreatedAt,
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting expired products");
                throw new Exception("Error while getting expired products");
            }
        }
    }
}

