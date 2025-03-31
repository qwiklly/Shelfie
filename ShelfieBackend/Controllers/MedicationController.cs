using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;
using static ShelfieBackend.Responses.CustomResponses;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Route("api/medication")]
    [ApiController]
    public class MedicationController(IMedicineRepo _medicineRepository) : ControllerBase
    {
        [Authorize]
        [HttpPost("add")]
        [SwaggerOperation(Summary = "Add a new medicament", Description = "Adds a new medicament to the inventory.")]
        public async Task<ActionResult<BaseResponse>> AddMedicamentAsync([FromBody] AddProductDTO model)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _medicineRepository.AddMedicamentAsync(model, currentUser, cancellationToken);
            return Ok(result);
        }
        [Authorize]
        [HttpGet("getAll")]
        [SwaggerOperation(Summary = "Get all medicaments", Description = "Retrieves all medicaments.")]
        public async Task<ActionResult<BaseResponse>> GetMedicamentsAsync()
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _medicineRepository.GetMedicamentsAsync(currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("get/{name}")]
        [SwaggerOperation(Summary = "Get medicament by name", Description = "Retrieves a medicaments by its name.")]
        public async Task<ActionResult<BaseResponse>> GetMedicamentAsync(string name)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _medicineRepository.GetMedicamentAsync(name, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpPut("update/{currentName}")]
        [SwaggerOperation(Summary = "Update a product", Description = "Updates a product's details.")]
        public async Task<ActionResult<BaseResponse>> UpdateMedicamentAsync(string currentName, [FromBody] UpdateProductDTO model)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _medicineRepository.UpdateMedicamentAsync(currentName, model, currentUser, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("delete/{name}")]
        [SwaggerOperation(Summary = "Delete a medicament", Description = "Deletes a medicament by its name.")]
        public async Task<ActionResult<BaseResponse>> DeleteMedicamentAsync(string name)
        {
            var currentUser = HttpContext.User;
            var cancellationToken = HttpContext.RequestAborted;
            var result = await _medicineRepository.DeleteMedicamentAsync(name, currentUser, cancellationToken);
            return Ok(result);
        }
    }
}
