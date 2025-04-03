using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using Swashbuckle.AspNetCore.Annotations;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
    [Route("api/application")]
    [ApiController]
    public class ProductController(IProductRepo _productRepo) : ControllerBase
    {

        [Authorize]
        [HttpPost("add")]
        [SwaggerOperation(Summary = "Add a new product", Description = "Adds a new product to the inventory.")]
        public async Task<IActionResult> AddProductAsync([FromBody] AddProductDTO model)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.AddProductAsync(model, currentUser, cancellationToken);
            return Ok(result);
        }
        [Authorize]
        [HttpGet("getAll")]
        [SwaggerOperation(Summary = "Get all products", Description = "Retrieves all products.")]
        public async Task<IActionResult> GetProductsAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.GetProductsAsync(currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("get/{name}")]
        [SwaggerOperation(Summary = "Get product by name", Description = "Retrieves a product by its name.")]
        public async Task<IActionResult> GetProductAsync(string name)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.GetProductAsync(name, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpPut("update/{currentName}")]
        [SwaggerOperation(Summary = "Update a product", Description = "Updates a product's details.")]
        public async Task<IActionResult> UpdateProductAsync(string currentName, [FromBody] UpdateProductDTO model)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.UpdateProductAsync(currentName, model, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("delete/{name}")]
        [SwaggerOperation(Summary = "Delete a product", Description = "Deletes a product by its name.")]
        public async Task<IActionResult> DeleteProductAsync(string name)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.DeleteProductAsync(name, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("getExpiredProducts")]
        [SwaggerOperation(Summary = "Get all expired products", Description = "Retrieves all expired products.")]
        public async Task<IActionResult> GetExpiredProducts()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _productRepo.GetExpiredProductsAsync(currentUser, cancellationToken);
            return Ok(result);
        }
    }
}
