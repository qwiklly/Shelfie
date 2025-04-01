using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using static ShelfieBackend.Responses.CustomResponses;
using System.Security.Claims;

namespace ShelfieBackend.Repositories.Interfaces
{
    public interface IMedicineRepo
    {
        Task<BaseResponse> AddMedicamentAsync(AddProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<GetProductDTO>> GetMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<Medication?> GetMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> DeleteMedicamentAsync(string name, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<BaseResponse> UpdateMedicamentAsync(string currentName, UpdateProductDTO model, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
        Task<List<GetProductDTO>> GetExpiredMedicamentsAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    }
}
