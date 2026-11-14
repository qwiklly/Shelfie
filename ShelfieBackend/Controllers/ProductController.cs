using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/application")]
    public class ProductController(IProductRepo _productRepo) : ApiControllerBase
    {
        [HttpPost("add")]
        [SwaggerOperation(Summary = "Add a new product", Description = "Adds a new product to the inventory.")]
        public async Task<IActionResult> AddProductAsync([FromBody] AddProductDTO model)
        {
            var result = await _productRepo.AddProductAsync(model, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpGet("getAll")]
        [SwaggerOperation(Summary = "Get all products", Description = "Retrieves all products.")]
        public async Task<IActionResult> GetProductsAsync()
        {
            var result = await _productRepo.GetProductsAsync(User, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("get/{name}")]
        [SwaggerOperation(Summary = "Get product by name", Description = "Retrieves a product by its name.")]
        public async Task<IActionResult> GetProductAsync(string name)
        {
            var result = await _productRepo.GetProductAsync(name, User, HttpContext.RequestAborted);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpPut("update/{currentName}")]
        [SwaggerOperation(Summary = "Update a product", Description = "Updates a product's details.")]
        public async Task<IActionResult> UpdateProductAsync(string currentName, [FromBody] UpdateProductDTO model)
        {
            var result = await _productRepo.UpdateProductAsync(currentName, model, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpDelete("delete/{name}")]
        [SwaggerOperation(Summary = "Delete a product", Description = "Deletes a product by its name.")]
        public async Task<IActionResult> DeleteProductAsync(string name)
        {
            var result = await _productRepo.DeleteProductAsync(name, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpGet("getExpiredProducts")]
        [SwaggerOperation(Summary = "Get all expired products", Description = "Retrieves all expired products.")]
        public async Task<IActionResult> GetExpiredProducts()
        {
            var result = await _productRepo.GetExpiredProductsAsync(User, HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
