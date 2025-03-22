using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
    [ApiController]
    [Route("api/category/{categoryId:int}")]
    public class CategoryFieldsController(ICategoryFieldRepo _categoryFieldRepo) : ControllerBase
    {

        [HttpGet("getCategoryFields")]
        public async Task<IActionResult> GetCategoryFields(int categoryId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldRepo.GetCategoryFieldsAsync(categoryId, User, cancellationToken);
            return Ok(response);
        }

        [HttpPost("addOrChangeFields")]
        public async Task<IActionResult> SaveCategoryFields(int categoryId, [FromBody] List<string> fieldNames)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldRepo.SaveCategoryFieldsAsync(categoryId, fieldNames, User, cancellationToken);
            return Ok(response);
        }
    }
}
