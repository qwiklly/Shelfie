using Microsoft.EntityFrameworkCore;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Services;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class HistoryRepo : IHistoryRepo
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IUserIdService _userService;

        public HistoryRepo(ApplicationDbContext appDbContext, IUserIdService userService)
        {
            _appDbContext = appDbContext;
            _userService = userService;
        }

        public async Task AddHistoryRecordAsync(HistoryRecordDTO model, CancellationToken cancellationToken)
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
            await _appDbContext.HistoryRecords.AddAsync(history, cancellationToken);
            await _appDbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<HistoryRecordDTO>> GetUserHistoryAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            int? userId = _userService.GetUserId(currentUser);
            if (userId == null)
                return new List<HistoryRecordDTO>();

            return await _appDbContext.HistoryRecords
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(h => h.ChangeDate)
                .ThenByDescending(h => h.Id)
                .Select(h => new HistoryRecordDTO
                {
                    Id = h.Id,
                    ItemId = h.ItemId,
                    ItemType = h.ItemType,
                    ItemName = h.ItemName,
                    ChangeDate = h.ChangeDate,
                    ChangeType = h.ChangeType,
                    QuantityChange = h.QuantityChange,
                    UserId = h.UserId,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<BaseResponse> ClearHistoryAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new BaseResponse(false, "Пользователь не найден.");

                // Only the history of the current user — never the whole table.
                await _appDbContext.HistoryRecords
                    .Where(h => h.UserId == userId.Value)
                    .ExecuteDeleteAsync(cancellationToken);

                return new BaseResponse(true, "История успешно очищена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при очистке истории");
                return new BaseResponse(false, "Ошибка при очистке истории");
            }
        }
    }
}
