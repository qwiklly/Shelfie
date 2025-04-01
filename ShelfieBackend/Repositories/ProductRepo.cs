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
    public class ProductRepo : IProductRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IHistoryRepo _historyRepository;   
        private readonly IUserIdService _userService;

        public ProductRepo(ApplicationDbContext appDbContext, IConfiguration config, IHistoryRepo historyRepository, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _historyRepository = historyRepository;
            _userService = userService;
        }

        public async Task<BaseResponse> AddProductAsync(AddProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var category = await _appDbContext.Categories.FindAsync(model.CategoryId, cancellationToken);
                var categoryId = category != null ? model.CategoryId : 1;

                var product = new Product
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
                await _appDbContext.Products.AddAsync(product, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = product.Name, 
                    ItemId = product.Id,
                    CategoryId = product.CategoryId,
                    ChangeType = "Added",
                    QuantityChange = product.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Продукт успешно добавлен." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while adding a product");
                return new BaseResponse(false, "Error while adding a product");
            }
        }

        public async Task<List<GetProductDTO>> GetProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<GetProductDTO>();

                return await _appDbContext.Products
                    .Where(p => p.UserId == userId.Value)
                    .Select(p => new GetProductDTO
                    {
                        Name = p.Name,
                        Creator = p.Creator,
                        CategoryId = p.CategoryId,
                        ExpirationDate = p.ExpirationDate,
                        Quantity = p.Quantity,
                        CreatedAt = p.CreatedAt,
                        Weight = p.Weight,
                        WeightUnit = p.WeightUnit,
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting products");
                throw new Exception("Error while getting products");
            }
        }

        public async Task<Product?> GetProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return null;

            return await _appDbContext.Products
                .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
        }

        public async Task<BaseResponse> DeleteProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
                if (product == null)
                    return new BaseResponse { Flag = false, Message = "Продукт не найден." };

                // Сохранить количество для истории
                int quantity = product.Quantity;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = product.Creator,
                    ItemId = product.Id,
                    ChangeType = "Removed",
                    QuantityChange = -quantity,
                    UserId = userId.Value
                }, cancellationToken);

                _appDbContext.Products.Remove(product);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Продукт успешно удалён." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while deleting a product");
                return new BaseResponse(false, "Error while deleting a product");
            }
        }

        public async Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value, cancellationToken);
                if (product == null)
                    return new BaseResponse { Flag = false, Message = "Продукт не найден." };

                int oldQuantity = product.Quantity;

                product.Name = model.Name ?? product.Name;
                product.Creator = model.Creator ?? product.Creator;
                product.ExpirationDate = model.ExpirationDate ?? product.ExpirationDate;
                product.Quantity = model.Quantity ?? product.Quantity;
                product.Weight = model.Weight == 0 ? product.Weight : model.Weight;
                product.WeightUnit = model.WeightUnit ?? product.WeightUnit;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);
                int quantityChange = product.Quantity - oldQuantity;

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = product.Name,
                    ItemId = product.Id,
                    ChangeType = "Updated",
                    QuantityChange = quantityChange,
                    UserId = userId.Value
                }, cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse { Flag = true, Message = "Продукт успешно обновлён." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a product");
                return new BaseResponse(false, "Error while updating a product");
            }
        }

        public async Task<List<GetProductDTO>> GetExpiredProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<GetProductDTO>();

                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

                return await _appDbContext.Products
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
