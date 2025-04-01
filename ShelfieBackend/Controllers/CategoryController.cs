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
        [SwaggerOperation(Summary = "Add new Category", Description = "adding new Category.")]
        public async Task<IActionResult> CreateCategoryAsync([FromBody] AddCategoryDTO category)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.CreateCategoryAsync(category.Name, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getAllCategories")]
        [SwaggerOperation(Summary = "Get all Categories", Description = "getting all categories.")]
        public async Task<IActionResult> GetAllCategoriesAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.GetAllCategories(currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getOneCategory/{categoryId}")]
        [SwaggerOperation(Summary = "Get one category", Description = "getting one Category.")]
        public async Task<IActionResult> GetOneCategoryAsync(int categoryId)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.GetCategoryAsync(categoryId, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{categoryId}")]
        [SwaggerOperation(Summary = "Delete the category", Description = "deleting the Category.")]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _categoryRepository.DeleteCategoryAsync(categoryId, User, cancellationToken);
            return Ok(result);
        }
    }
}