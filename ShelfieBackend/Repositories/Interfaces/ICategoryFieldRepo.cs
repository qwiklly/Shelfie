using ShelfieBackend.Models;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface ICategoryFieldRepo
    {
        Task<BaseResponse> SaveCategoryFieldsAsync(int categoryId, List<string> fieldNames, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<string>> GetCategoryFieldsAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    }
}
