using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/category")]
    public class CategoryController(ICategoryRepo _categoryRepository) : ApiControllerBase
    {
        [HttpPost("createCategory")]
        [SwaggerOperation(Summary = "Add new Category", Description = "adding new Category.")]
        public async Task<IActionResult> CreateCategoryAsync([FromBody] AddCategoryDTO category)
        {
            var result = await _categoryRepository.CreateCategoryAsync(category.Name, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpGet("getAllCategories")]
        [SwaggerOperation(Summary = "Get all Categories", Description = "getting all categories of the current user.")]
        public async Task<IActionResult> GetAllCategoriesAsync()
        {
            var result = await _categoryRepository.GetAllCategories(User, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("getOneCategory/{categoryId:int}")]
        [SwaggerOperation(Summary = "Get one category", Description = "getting one Category.")]
        public async Task<IActionResult> GetOneCategoryAsync(int categoryId)
        {
            var result = await _categoryRepository.GetCategoryAsync(categoryId, User, HttpContext.RequestAborted);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpDelete("{categoryId:int}")]
        [SwaggerOperation(Summary = "Delete the category", Description = "deleting the Category with its fields and records.")]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            var result = await _categoryRepository.DeleteCategoryAsync(categoryId, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }
    }
}
