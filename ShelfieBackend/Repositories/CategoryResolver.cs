using Microsoft.EntityFrameworkCore;
using ShelfieBackend.Data;

namespace ShelfieBackend.Repositories
{
    internal static class CategoryResolver
    {
        /// <summary>
        /// Returns <paramref name="requestedId"/> when it points to a predefined category or to one owned by the user,
        /// otherwise <paramref name="fallbackId"/>. Prevents attaching items to a category of another user.
        /// </summary>
        public static async Task<int> ResolveAsync(ApplicationDbContext db, int requestedId, int userId, int fallbackId, CancellationToken cancellationToken)
        {
            if (requestedId <= 0)
                return fallbackId;

            bool allowed = await db.Categories
                .AnyAsync(c => c.Id == requestedId && (c.UserId == null || c.UserId == userId), cancellationToken);

            return allowed ? requestedId : fallbackId;
        }
    }
}
