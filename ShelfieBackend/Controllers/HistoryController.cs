using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class HistoryController(IHistoryRepo _historyRepo) : ControllerBase
    {

        [Authorize]
        [HttpGet("getAllHistory")]
        [SwaggerOperation(Summary = "Get all history", Description = "Retrieves all items history.")]
        public async Task<IActionResult> GetAllHistoryAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _historyRepo.GetUserHistoryAsync(currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("clear")]
        [SwaggerOperation(Summary = "Clear all history", Description = "Clear all items history.")]
        public async Task<IActionResult> ClearHistory()
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _historyRepo.ClearHistoryAsync(cancellationToken);
            return Ok(result);
        }

    }
}