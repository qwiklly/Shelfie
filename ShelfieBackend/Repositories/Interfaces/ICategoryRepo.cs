using ShelfieBackend.DTOs;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface ICategoryRepo
    {
        Task<BaseResponse> CreateCategoryAsync(string categoryName, ClaimsPrincipal currentUser);
        Task<List<GetCategoriesDTO>> GetAllCategories(ClaimsPrincipal currentUser);
    }
}
