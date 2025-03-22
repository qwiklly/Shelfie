using System.Security.Claims;

namespace ShelfieBackend.Services
{
    public class UserIdService : IUserIdService
    {
        public int? GetUserId(ClaimsPrincipal user)
        {
            return int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) && userId > 0
                ? userId
                : null;
        }
    }
}
