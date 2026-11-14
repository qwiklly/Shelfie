using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/categoryValue")]
    public class CategoryFieldValueController(ICategoryFieldValueRepo _categoryFieldValueRepo) : ApiControllerBase
    {
        [HttpPost("{categoryId:int}/save-values")]
        [SwaggerOperation(
            Summary = "Save field values",
            Description = "Adds a record. Send RecordId = 0 to let the server pick the next id.")]
        public async Task<IActionResult> SaveFieldValues(int categoryId, [FromBody] FieldValuesRequest request)
        {
            var response = await _categoryFieldValueRepo.PostFieldValuesAsync(categoryId, request, User, HttpContext.RequestAborted);
            return FromResponse(response);
        }

        [HttpPut("{categoryId:int}/update-values")]
        [SwaggerOperation(Summary = "Update field values", Description = "Updates existing field values for a category.")]
        public async Task<IActionResult> UpdateFieldValues(int categoryId, [FromBody] FieldValuesRequest request)
        {
            var response = await _categoryFieldValueRepo.UpdateFieldValuesAsync(categoryId, request, User, HttpContext.RequestAborted);
            return FromResponse(response);
        }

        [HttpGet("{categoryId:int}/values")]
        [SwaggerOperation(Summary = "Get field values", Description = "Retrieves all records of a category.")]
        public async Task<IActionResult> GetFieldValues(int categoryId)
        {
            var values = await _categoryFieldValueRepo.GetFieldValuesAsync(categoryId, User, HttpContext.RequestAborted);
            return Ok(values);
        }

        [HttpDelete("{categoryId:int}/{recordId:int}")]
        [SwaggerOperation(Summary = "Delete field values", Description = "Deletes a record by ID.")]
        public async Task<IActionResult> DeleteFieldValues(int categoryId, int recordId)
        {
            var response = await _categoryFieldValueRepo.DeleteFieldValuesAsync(categoryId, recordId, User, HttpContext.RequestAborted);
            return FromResponse(response);
        }
    }
}
