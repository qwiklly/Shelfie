using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [ApiController]
    [Route("api/category/{categoryId:int}")]
    public class CategoryFieldsController(ICategoryFieldRepo _categoryFieldRepo) : ControllerBase
    {

        [HttpGet("getCategoryFields")]
        [SwaggerOperation(
            Summary = "Get category fields",
            Description = "Retrieves all fields associated with a specific category."
        )]
        public async Task<IActionResult> GetCategoryFields(int categoryId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldRepo.GetCategoryFieldsAsync(categoryId, User, cancellationToken);
            return Ok(response);
        }

        [HttpPost("addOrChangeFields")]
        [SwaggerOperation(
            Summary = "Add or update category fields",
            Description = "Adds new fields or updates existing fields for a category."
        )]
        public async Task<IActionResult> SaveCategoryFields(int categoryId, [FromBody] List<string> fieldNames)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldRepo.SaveCategoryFieldsAsync(categoryId, fieldNames, User, cancellationToken);
            return Ok(response);
        }
    }
}
