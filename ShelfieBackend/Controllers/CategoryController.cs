using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static ShelfieBackend.Responses.CustomResponses;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.DTOs;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class CategoryController(ICategoryRepo _categoryRepository) : ControllerBase
    {

        [Authorize]
        [HttpPost("createCategory")]
        [SwaggerOperation(Summary = "add new Category", Description = "adding new Category.")]
        public async Task<ActionResult<BaseResponse>> CreateCategoryAsync([FromBody] addCategoryDTO category)
        {
            var currentUser = HttpContext.User;
            var result = await _categoryRepository.CreateCategoryAsync(category.Name, currentUser);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getAllCategories")]
        [SwaggerOperation(Summary = "add new Category", Description = "adding new Category.")]
        public async Task<ActionResult<BaseResponse>> GetAllCategoriesAsync()
        {
            var currentUser = HttpContext.User;
            var result = await _categoryRepository.GetAllCategories(currentUser);
            return Ok(result);
        }
    }
}