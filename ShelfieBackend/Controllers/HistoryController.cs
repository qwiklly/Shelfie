using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class HistoryController(IHistoryRepo _historytRepo) : ControllerBase
    {

        [Authorize]
        [HttpGet("getAllHistory")]
        [SwaggerOperation(Summary = "Get all history", Description = "Retrieves all product history.")]
        public async Task<ActionResult<List<HistoryRecordDTO>>> GetAllHistoryAsync()
        {
            var currentUser = HttpContext.User;
            var result = await _historytRepo.GetUserHistoryAsync(currentUser);
            return Ok(result);
        }
    }
}