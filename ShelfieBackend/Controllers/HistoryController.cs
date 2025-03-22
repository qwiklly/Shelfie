using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.Repositories;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class HistoryController(IHistoryRepo _historyRepo) : ControllerBase
    {

        [Authorize]
        [HttpGet("getAllHistory")]
        [SwaggerOperation(Summary = "Get all history", Description = "Retrieves all product history.")]
        public async Task<ActionResult<List<HistoryRecordDTO>>> GetAllHistoryAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _historyRepo.GetUserHistoryAsync(currentUser, cancellationToken);
            return Ok(result);
        }
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearHistory()
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _historyRepo.ClearHistoryAsync(cancellationToken);
            return Ok(result);
        }

    }
}