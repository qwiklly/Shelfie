using ShelfieBackend.DTOs;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface ICategoryRepo
    {
        Task<BaseResponse> CreateCategoryAsync(string categoryName, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<GetCategoriesDTO>> GetAllCategories(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> DeleteCategoryAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    }
}
