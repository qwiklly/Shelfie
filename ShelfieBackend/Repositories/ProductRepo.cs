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
    public class ProductRepo : IProductRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IHistoryRepo _historyRepository;
        private readonly IUserIdService _userService;

        public ProductRepo(ApplicationDbContext appDbContext, IHistoryRepo historyRepository, IUserIdService userService)
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
                    return new BaseResponse(false, "Пользователь не найден.");

                var name = model.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                    return new BaseResponse(false, "Название продукта не может быть пустым.");

                // Products are addressed by name in the API, so names must be unique per user.
                bool exists = await _appDbContext.Products
                    .AnyAsync(p => p.UserId == userId.Value && p.Name == name, cancellationToken);
                if (exists)
                    return new BaseResponse(false, "Продукт с таким названием уже существует.");

                var categoryId = await CategoryResolver.ResolveAsync(_appDbContext, model.CategoryId, userId.Value, Constants.ProductsCategoryId, cancellationToken);

                var product = new Product
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
                await _appDbContext.Products.AddAsync(product, cancellationToken);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeProduct,
                    ItemName = product.Name,
                    ItemId = product.Id,
                    CategoryId = product.CategoryId,
                    ChangeType = Constants.ChangeAdded,
                    QuantityChange = product.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Продукт успешно добавлен.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while adding a product");
                return new BaseResponse(false, "Error while adding a product");
            }
        }

        public async Task<List<GetProductDTO>> GetProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<GetProductDTO>();

            return await _appDbContext.Products
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .OrderBy(p => p.Name)
                .Select(ToDto)
                .ToListAsync(cancellationToken);
        }

        public async Task<GetProductDTO?> GetProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return null;

            return await _appDbContext.Products
                .AsNoTracking()
                .Where(p => p.Name == name && p.UserId == userId.Value)
                .Select(ToDto)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<BaseResponse> DeleteProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден.");

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value, cancellationToken);
                if (product == null)
                    return new BaseResponse(false, "Продукт не найден.");

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeProduct,
                    ItemName = product.Name,
                    ItemId = product.Id,
                    CategoryId = product.CategoryId,
                    ChangeType = Constants.ChangeRemoved,
                    QuantityChange = -product.Quantity,
                    UserId = userId.Value
                }, cancellationToken);

                _appDbContext.Products.Remove(product);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Продукт успешно удалён.");
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
                    return new BaseResponse(false, "Пользователь не найден.");

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value, cancellationToken);
                if (product == null)
                    return new BaseResponse(false, "Продукт не найден.");

                if (model.Quantity < 0 || model.Weight < 0)
                    return new BaseResponse(false, "Количество и вес не могут быть отрицательными.");

                var newName = string.IsNullOrWhiteSpace(model.Name) ? product.Name : model.Name.Trim();
                if (newName != product.Name &&
                    await _appDbContext.Products.AnyAsync(p => p.UserId == userId.Value && p.Name == newName, cancellationToken))
                {
                    return new BaseResponse(false, "Продукт с таким названием уже существует.");
                }

                int oldQuantity = product.Quantity;

                product.Name = newName;
                product.Creator = model.Creator?.Trim() ?? product.Creator;
                product.Description = model.Description ?? product.Description;
                product.ExpirationDate = model.ExpirationDate ?? product.ExpirationDate;
                product.Quantity = model.Quantity ?? product.Quantity;
                product.Weight = model.Weight is null or 0 ? product.Weight : model.Weight.Value;
                product.WeightUnit = string.IsNullOrWhiteSpace(model.WeightUnit) ? product.WeightUnit : model.WeightUnit;

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync(cancellationToken);

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = Constants.ItemTypeProduct,
                    ItemName = product.Name,
                    ItemId = product.Id,
                    CategoryId = product.CategoryId,
                    ChangeType = Constants.ChangeUpdated,
                    QuantityChange = product.Quantity - oldQuantity,
                    UserId = userId.Value
                }, cancellationToken);

                await _appDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new BaseResponse(true, "Продукт успешно обновлён.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a product");
                return new BaseResponse(false, "Error while updating a product");
            }
        }

        public async Task<List<GetProductDTO>> GetExpiredProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<GetProductDTO>();

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await _appDbContext.Products
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value && p.ExpirationDate != null && p.ExpirationDate < today)
                .OrderBy(p => p.ExpirationDate)
                .Select(ToDto)
                .ToListAsync(cancellationToken);
        }

        private static readonly Expression<Func<Product, GetProductDTO>> ToDto = p => new GetProductDTO
        {
            Id = p.Id,
            Name = p.Name,
            Creator = p.Creator,
            CategoryId = p.CategoryId,
            ExpirationDate = p.ExpirationDate,
            Quantity = p.Quantity,
            CreatedAt = p.CreatedAt,
            Description = p.Description,
            Weight = p.Weight,
            WeightUnit = p.WeightUnit,
        };
    }
}
