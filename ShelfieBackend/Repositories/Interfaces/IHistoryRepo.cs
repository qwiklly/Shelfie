using ShelfieBackend.DTOs;
using System.Security.Claims;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface IHistoryRepo
    {
        Task AddHistoryRecordAsync(HistoryRecordDTO model);
        Task<List<HistoryRecordDTO>> GetUserHistoryAsync(ClaimsPrincipal currentUser);
    }
}
