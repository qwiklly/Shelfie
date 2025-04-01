using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface IProductRepo
    {
        Task<BaseResponse> AddProductAsync(AddProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<GetProductDTO>> GetProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<Product?> GetProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> DeleteProductAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<GetProductDTO>> GetExpiredProductsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    }
}
