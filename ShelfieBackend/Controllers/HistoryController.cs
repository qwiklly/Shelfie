using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.Repositories;
using ShelfieBackend.DTOs;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class HistoryController(IProductRepo _productRepo) : ControllerBase
    {

        [Authorize]
        [HttpGet("getAllHistory")]
        [SwaggerOperation(Summary = "Get all history", Description = "Retrieves all product history.")]
        public async Task<ActionResult<List<ProductHistoryDTO>>> GetAllHistoryAsync()
        {
            var currentUser = HttpContext.User;
            var result = await _productRepo.GetAllHistory(currentUser);
            return Ok(result);
        }
    }
}