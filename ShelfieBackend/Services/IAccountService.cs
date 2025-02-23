using ShelfieBackend.DTOs;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Services
{
    public interface IAccountService
    {
        Task<BaseResponse> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<RegisterResponse> RegisterAsync(RegisterDTO model, CancellationToken cancellationToken = default);
        Task<LoginResponse> LoginAsync(LoginDTO model, CancellationToken cancellationToken = default);
        Task<BaseResponse> DeleteUserAsync(string email, CancellationToken cancellationToken = default);
        Task<BaseResponse> UpdateUserAsync(RegisterDTO model, CancellationToken cancellationToken = default);
        Task<BaseResponse> GetUserAsync(string email, CancellationToken cancellationToken = default);
    }
}
