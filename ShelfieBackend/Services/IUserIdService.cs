using System.Security.Claims;

namespace ShelfieBackend.Services
{
    public interface IUserIdService
    {
        int? GetUserId(ClaimsPrincipal user);
    }
}
