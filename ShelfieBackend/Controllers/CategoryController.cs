using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.DTOs;

namespace ShelfieBackend.Controllers
{
    [Route("api/category")]
    [ApiController]
    public class CategoryController(ICategoryRepo _categoryRepository) : ControllerBase
    {

        [Authorize]
        [HttpPost("createCategory")]
        [SwaggerOperation(Summary = "add new Category", Description = "adding new Category.")]
        public async Task<IActionResult> CreateCategoryAsync([FromBody] addCategoryDTO category)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.CreateCategoryAsync(category.Name, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getAllCategories")]
        [SwaggerOperation(Summary = "add new Category", Description = "adding new Category.")]
        public async Task<ActionResult<GetCategoriesDTO>> GetAllCategoriesAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.GetAllCategories(currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getOneCategory/{categoryId}")]
        [SwaggerOperation(Summary = "add new Category", Description = "adding new Category.")]
        public async Task<ActionResult<GetCategoriesDTO>> GetOneCategoryAsync(int categoryId)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.GetCategoryAsync(categoryId, currentUser, cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{categoryId}")]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.DeleteCategoryAsync(categoryId, User, cancellationToken);
            return Ok(result);
        }
    }
}