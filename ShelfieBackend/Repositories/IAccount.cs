using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
	public interface IAccount
	{
		Task<RegisterResponse> RegisterAsync(RegisterDTO model, ClaimsPrincipal currentUser);
		Task<LoginResponse> LoginAsync(LoginDTO model);
		Task<List<GetUsersDTO>> GetUsersAsync();
		Task<ApplicationUser?> GetUserAsync(string email);
		Task<BaseResponse> DeleteUserAsync(DeleteUserDTO model);
		Task<RegisterResponse> UpdateUserAsync(string email, RegisterDTO model, ClaimsPrincipal currentUser);
	}
}
