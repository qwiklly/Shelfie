using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class ProductRepo : IProductRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IConfiguration _config;
        private readonly IHistoryRepo _historyRepository;   

        public ProductRepo(ApplicationDbContext appDbContext, IConfiguration config, IHistoryRepo historyRepository)
        {
            _appDbContext = appDbContext;
            _config = config;
            _historyRepository = historyRepository;
        }

        public async Task<BaseResponse> AddProductAsync(AddProductDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                // Получаем идентификатор пользователя
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                // Проверяем наличие указанной категории
                var category = await _appDbContext.Categories.FindAsync(model.CategoryId);
                if (category == null)
                    model.CategoryId = 1;

                var product = new Product
                {
                    Name = model.Name,
                    Creator = model.Creator,
                    CategoryId = 1,
                    ExpirationDate = model.ExpirationDate,
                    Quantity = model.Quantity,
                    UserId = userId.Value,
                    CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
                };

                // Используем транзакцию, чтобы сохранить продукт и запись истории атомарно
                using var transaction = await _appDbContext.Database.BeginTransactionAsync();
                await _appDbContext.Products.AddAsync(product);
                await _appDbContext.SaveChangesAsync();

                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = product.Creator,
                    ItemId = product.Id,
                    CategoryId = product.CategoryId, 
                    ChangeType = "Added",
                    QuantityChange = product.Quantity, 
                    UserId = userId.Value
                });
                await transaction.CommitAsync();

                return new BaseResponse { Flag = true, Message = "Продукт успешно добавлен." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while adding a product");
                return new BaseResponse(false, "Error while adding a product");
            }
        }


        public async Task<List<GetProductDTO>> GetProductsAsync(ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new List<GetProductDTO>();

                // Используем проекцию на уровне базы данных для оптимизации
                return await _appDbContext.Products
                    .Where(p => p.UserId == userId.Value)
                    .Select(p => new GetProductDTO
                    {
                        Name = p.Name,
                        Creator = p.Creator,
                        CategoryId = p.CategoryId,
                        ExpirationDate = p.ExpirationDate,
                        Quantity = p.Quantity,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting products");
                throw new Exception("Error while getting products");
            }
        }

        public async Task<Product?> GetProductAsync(string name, ClaimsPrincipal currentUser)
        {
            int? userId = GetUserId(currentUser);
            if (userId == null)
                return null;

            // Фильтруем по имени и по идентификатору пользователя
            return await _appDbContext.Products
                .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value);
        }

        public async Task<BaseResponse> DeleteProductAsync(string name, ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == name && p.UserId == userId.Value);
                if (product == null)
                    return new BaseResponse { Flag = false, Message = "Продукт не найден." };

                // Сохраним количество для истории
                int quantity = product.Quantity;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync();

                // Добавляем запись истории до удаления продукта
                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Products",
                    ItemName = product.Creator,
                    ItemId = product.Id,
                    ChangeType = "Removed",
                    QuantityChange = -quantity,
                    UserId = userId.Value
                });
                // Удаляем продукт
                _appDbContext.Products.Remove(product);
                await _appDbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                return new BaseResponse { Flag = true, Message = "Продукт успешно удалён." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while deleting a product");
                return new BaseResponse(false, "Error while deleting a product");
            }
        }


        public async Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value);
                if (product == null)
                    return new BaseResponse { Flag = false, Message = "Продукт не найден." };

                int oldQuantity = product.Quantity;

                // Обновляем свойства, если они не null/пустые
                product.Name = model.Name ?? product.Name;
                product.Creator = model.Creator ?? product.Creator;
                product.ExpirationDate = model.ExpirationDate ?? product.ExpirationDate;
                product.Quantity = model.Quantity ?? product.Quantity;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync();
                await _appDbContext.SaveChangesAsync();

                int quantityChange = product.Quantity - oldQuantity;
                await _historyRepository.AddHistoryRecordAsync(new HistoryRecordDTO
                {
                    ItemType = "Product",
                    ItemName = product.Name,
                    ItemId = product.Id,
                    ChangeType = "Updated",
                    QuantityChange = quantityChange,
                    UserId = userId.Value
                });

                await transaction.CommitAsync();

                return new BaseResponse { Flag = true, Message = "Продукт успешно обновлён." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a product");
                return new BaseResponse(false, "Error while updating a product");
            }
        }

        // Метод извлекает идентификатор пользователя из Claims. Если идентификатор отсутствует, неверен или равен 0, возвращает null.
        protected internal int? GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || userId == 0)
                return null;
            return userId;
        }
    }
}
