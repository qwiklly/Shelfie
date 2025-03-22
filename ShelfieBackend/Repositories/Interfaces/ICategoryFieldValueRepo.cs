using static ShelfieBackend.Responses.CustomResponses;
using System.Security.Claims;
using ShelfieBackend.Models;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface ICategoryFieldValueRepo
    {
        Task<BaseResponse> PostFieldValuesAsync(int categoryId, FieldValuesRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> UpdateFieldValuesAsync(int categoryId, FieldValuesRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<FieldValuesRequest>> GetFieldValuesAsync(int categoryId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> DeleteFieldValuesAsync(int categoryId, int recordId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    }
}
