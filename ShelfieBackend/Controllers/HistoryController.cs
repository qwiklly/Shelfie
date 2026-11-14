using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/application")]
    public class HistoryController(IHistoryRepo _historyRepo) : ApiControllerBase
    {
        [HttpGet("getAllHistory")]
        [SwaggerOperation(Summary = "Get all history", Description = "History of the current user, newest first.")]
        public async Task<IActionResult> GetAllHistoryAsync()
        {
            var result = await _historyRepo.GetUserHistoryAsync(User, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpDelete("clear")]
        [SwaggerOperation(Summary = "Clear history", Description = "Clears the history of the current user.")]
        public async Task<IActionResult> ClearHistory()
        {
            var result = await _historyRepo.ClearHistoryAsync(User, HttpContext.RequestAborted);
            return FromResponse(result);
        }
    }
}
