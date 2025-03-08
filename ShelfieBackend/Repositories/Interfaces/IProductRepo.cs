using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface IProductRepo
    {
        Task<BaseResponse> AddProductAsync(AddProductDTO model, ClaimsPrincipal currentUser);
        Task<List<GetProductDTO>> GetProductsAsync(ClaimsPrincipal currentUser);
        Task<Product?> GetProductAsync(string name, ClaimsPrincipal currentUser);
        Task<BaseResponse> DeleteProductAsync(string name, ClaimsPrincipal currentUser);
        Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser);
    }
}
