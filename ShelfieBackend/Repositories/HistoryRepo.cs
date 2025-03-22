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
            await _appDbContext.HistoryRecords.AddAsync(history);
            await _appDbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<HistoryRecordDTO>> GetUserHistoryAsync(ClaimsPrincipal currentUser, CancellationToken cancellationToken)
        {
            try
            {
                int? userId = _userService.GetUserId(currentUser);
                if (userId == null)
                    return new List<HistoryRecordDTO>();

                return await _appDbContext.HistoryRecords
                    .AsNoTracking()
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
                .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting history operations");
                throw new Exception("Error while getting history operations", ex);
            }
        }

        public async Task<BaseResponse> ClearHistoryAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _appDbContext.HistoryRecords.ExecuteDeleteAsync(cancellationToken);
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