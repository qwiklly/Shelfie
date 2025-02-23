using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class ProductRepo : IProductRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IConfiguration _config;

        public ProductRepo(ApplicationDbContext appDbContext, IConfiguration config)
        {
            _appDbContext = appDbContext;
            _config = config;
        }

        public async Task<BaseResponse> AddProductAsync(AddProductDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                // ВРЕМЕННОЕ РЕШЕНИЕ: убедиться, что категория "Без категории" существует
                var defaultCategory = await _appDbContext.ProductCategories
                    .FirstOrDefaultAsync(c => c.Name == "Без категории");

                if (defaultCategory == null)
                {
                    defaultCategory = new ProductCategory { Id = 1, Name = "Без категории" };
                    await _appDbContext.ProductCategories.AddAsync(defaultCategory);
                    await _appDbContext.SaveChangesAsync();
                }

                // Получаем идентификатор пользователя
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse { Flag = false, Message = "Пользователь не найден." };

                // Проверяем наличие указанной категории
                var category = await _appDbContext.ProductCategories.FindAsync(model.CategoryId);
                if (category == null)
                    return new BaseResponse { Flag = false, Message = "Категория не найдена." };

                var product = new Product
                {
                    Name = model.Name,
                    CategoryId = model.CategoryId,
                    ExpirationDate = model.ExpirationDate,
                    Quantity = model.Quantity,
                    UserId = userId.Value,
                    CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
                };

                // Используем транзакцию, чтобы сохранить продукт и запись истории атомарно
                using var transaction = await _appDbContext.Database.BeginTransactionAsync();
                await _appDbContext.Products.AddAsync(product);
                await _appDbContext.SaveChangesAsync();

                await AddProductHistoryAsync(product.Id, "Added", product.Quantity, userId.Value);
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
                await AddProductHistoryAsync(product.Id, "Removed", -quantity, userId.Value);

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

                // Находим продукт по текущему имени и идентификатору пользователя
                var product = await _appDbContext.Products
                    .FirstOrDefaultAsync(p => p.Name == currentName && p.UserId == userId.Value);
                if (product == null)
                    return new BaseResponse { Flag = false, Message = "Продукт не найден." };

                int oldQuantity = product.Quantity;

                // Если передано новое имя, обновляем его
                if (!string.IsNullOrEmpty(model.Name))
                    product.Name = model.Name;

                if (model.CategoryId.HasValue)
                {
                    var category = await _appDbContext.ProductCategories.FindAsync(model.CategoryId.Value);
                    if (category == null)
                        return new BaseResponse { Flag = false, Message = "Категория не найдена." };

                    product.CategoryId = model.CategoryId.Value;
                }

                if (model.ExpirationDate.HasValue)
                    product.ExpirationDate = model.ExpirationDate;

                if (model.Quantity.HasValue)
                    product.Quantity = model.Quantity.Value;

                using var transaction = await _appDbContext.Database.BeginTransactionAsync();
                await _appDbContext.SaveChangesAsync();

                int quantityChange = product.Quantity - oldQuantity;
                await AddProductHistoryAsync(product.Id, "Updated", quantityChange, userId.Value);
                await transaction.CommitAsync();

                return new BaseResponse { Flag = true, Message = "Продукт успешно обновлён." };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating a product");
                return new BaseResponse(false, "Error while updating a product");
            }
        }

        public async Task<List<ProductHistoryDTO>> GetAllHistory(ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new List<ProductHistoryDTO>();

                // Возвращаем историю изменений только для текущего пользователя
                return await _appDbContext.ProductHistories
                    .Where(p => p.UserId == userId.Value)
                    .Select(p => new ProductHistoryDTO
                    {
                        Id = p.Id,
                        Product = p.Product != null ? p.Product.Name : "Unknown",
                        ProductId = p.ProductId,
                        ChangeDate = p.ChangeDate,
                        QuantityChange = p.QuantityChange,
                        UserId = p.UserId,
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting product history");
                throw new Exception("Error while getting product history");
            }
        }

        // Метод для создания записи истории изменений продукта.
        private async Task AddProductHistoryAsync(int productId, string changeType, int quantityChange, int userId)
        {
            var history = new ProductHistory
            {
                ProductId = productId,
                ChangeDate = DateTime.UtcNow,
                ChangeType = changeType,
                QuantityChange = quantityChange,
                UserId = userId
            };
            await _appDbContext.ProductHistories.AddAsync(history);
            await _appDbContext.SaveChangesAsync();
        }

        // Метод извлекает идентификатор пользователя из Claims. Если идентификатор отсутствует, неверен или равен 0, возвращает null.
        private int? GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || userId == 0)
                return null;
            return userId;
        }
    }
}
