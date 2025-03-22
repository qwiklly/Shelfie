using ShelfieBackend.DTOs;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface IHistoryRepo
    {
        Task AddHistoryRecordAsync(HistoryRecordDTO model, CancellationToken cancellationToken);
        Task<List<HistoryRecordDTO>> GetUserHistoryAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> ClearHistoryAsync(CancellationToken cancellationToken);
    }
}
