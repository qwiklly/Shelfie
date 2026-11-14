using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/category/{categoryId:int}")]
    public class CategoryFieldsController(ICategoryFieldRepo _categoryFieldRepo) : ApiControllerBase
    {
        [HttpGet("getCategoryFields")]
        [SwaggerOperation(
            Summary = "Get category field names",
            Description = "Names of the fields of a category, ordered by FieldOrder.")]
        public async Task<IActionResult> GetCategoryFields(int categoryId)
        {
            var response = await _categoryFieldRepo.GetCategoryFieldsAsync(categoryId, User, HttpContext.RequestAborted);
            return Ok(response);
        }

        [HttpGet("fields")]
        [SwaggerOperation(
            Summary = "Get category field definitions",
            Description = "Fields with their FieldOrder — the key used in record values.")]
        public async Task<IActionResult> GetCategoryFieldDefinitions(int categoryId)
        {
            var response = await _categoryFieldRepo.GetCategoryFieldDefinitionsAsync(categoryId, User, HttpContext.RequestAborted);
            return Ok(response);
        }

        [HttpPost("addOrChangeFields")]
        [SwaggerOperation(
            Summary = "Add or update category fields",
            Description = "Replaces the list of fields; fields are matched by name.")]
        public async Task<IActionResult> SaveCategoryFields(int categoryId, [FromBody] List<string> fieldNames)
        {
            var response = await _categoryFieldRepo.SaveCategoryFieldsAsync(categoryId, fieldNames, User, HttpContext.RequestAborted);
            return FromResponse(response);
        }
    }
}
