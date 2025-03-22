using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
    [Route("api/categoryValue")]
    [ApiController]
    public class CategoryFieldValueController(ICategoryFieldValueRepo _categoryFieldValueRepo) : ControllerBase
    {

        [HttpPost("{categoryId}/save-values")]
        public async Task<IActionResult> SaveFieldValues(int categoryId, [FromBody] FieldValuesRequest request)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldValueRepo.PostFieldValuesAsync(categoryId, request, User, cancellationToken);
            return Ok(response);
        }

        [HttpPut("{categoryId}/update-values")]
        public async Task<IActionResult> UpdateFieldValues(int categoryId, [FromBody] FieldValuesRequest request)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldValueRepo.UpdateFieldValuesAsync(categoryId, request, User, cancellationToken);
            return Ok(response);
        }

        [HttpGet("{categoryId}/values")]
        public async Task<IActionResult> GetFieldValues(int categoryId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var values = await _categoryFieldValueRepo.GetFieldValuesAsync(categoryId, User, cancellationToken);
            return Ok(values);
        }

        [HttpDelete("{categoryId}/{recordId}")]
        public async Task<IActionResult> DeleteFieldValues(int categoryId, int recordId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var response = await _categoryFieldValueRepo.DeleteFieldValuesAsync(categoryId, recordId, User, cancellationToken);
            return Ok(response);
        }
    }
}

