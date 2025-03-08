using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using System.Security.Claims;

namespace ShelfieBackend.Repositories
{
    public class HistoryRepo : IHistoryRepo
    {
        private readonly ApplicationDbContext _appDbContext;

        public HistoryRepo(ApplicationDbContext appDbContext) 
        { 
            _appDbContext = appDbContext;
        }

        public async Task AddHistoryRecordAsync(HistoryRecordDTO model)
        {
            var history = new HistoryRecord
            {
                ItemType = model.ItemType,
                ItemName = model.ItemName,
                ItemId = model.ItemId,
                ChangeDate = DateTime.UtcNow,
                ChangeType = model.ChangeType,
                QuantityChange = model.QuantityChange,
                UserId = model.UserId
            };
            await _appDbContext.HistoryRecords.AddAsync(history);
            await _appDbContext.SaveChangesAsync();
        }


        public async Task<List<HistoryRecordDTO>> GetUserHistoryAsync(ClaimsPrincipal currentUser)
        {
            try
            {
                int? userId = GetUserId(currentUser);
                if (userId == null)
                    return new List<HistoryRecordDTO>();

                // Возвращаем историю изменений только для текущего пользователя
                return await _appDbContext.HistoryRecords
                    .Where(p => p.UserId == userId.Value)
                     .Select(h => new HistoryRecordDTO
                     {
                         Id = h.Id,
                         ItemId = h.ItemId,
                         ItemType = h.ItemType,
                         ItemName = h.ItemName,
                         ChangeDate = h.ChangeDate,
                         ChangeType = h.ChangeType,
                         QuantityChange = h.QuantityChange,
                     })
                .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting product history");
                throw new Exception("Error while getting product history");
            }
        }
        protected internal int? GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || userId == 0)
                return null;
            return userId;
        }
    }
}