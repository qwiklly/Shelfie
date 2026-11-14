using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/medication")]
    public class MedicationController(IMedicineRepo _medicineRepository) : ApiControllerBase
    {
        [HttpPost("add")]
        [SwaggerOperation(Summary = "Add a new medicament", Description = "Adds a new medicament to the inventory.")]
        public async Task<IActionResult> AddMedicamentAsync([FromBody] AddProductDTO model)
        {
            var result = await _medicineRepository.AddMedicamentAsync(model, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpGet("getAll")]
        [SwaggerOperation(Summary = "Get all medicaments", Description = "Retrieves all medicaments.")]
        public async Task<IActionResult> GetMedicamentsAsync()
        {
            var result = await _medicineRepository.GetMedicamentsAsync(User, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("get/{name}")]
        [SwaggerOperation(Summary = "Get medicament by name", Description = "Retrieves a medicament by its name.")]
        public async Task<IActionResult> GetMedicamentAsync(string name)
        {
            var result = await _medicineRepository.GetMedicamentAsync(name, User, HttpContext.RequestAborted);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpPut("update/{currentName}")]
        [SwaggerOperation(Summary = "Update a medicine", Description = "Updates a medicine's details.")]
        public async Task<IActionResult> UpdateMedicamentAsync(string currentName, [FromBody] UpdateProductDTO model)
        {
            var result = await _medicineRepository.UpdateMedicamentAsync(currentName, model, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpDelete("delete/{name}")]
        [SwaggerOperation(Summary = "Delete a medicament", Description = "Deletes a medicament by its name.")]
        public async Task<IActionResult> DeleteMedicamentAsync(string name)
        {
            var result = await _medicineRepository.DeleteMedicamentAsync(name, User, HttpContext.RequestAborted);
            return FromResponse(result);
        }

        [HttpGet("getExpiredMedicaments")]
        [SwaggerOperation(Summary = "Get all expired medicines", Description = "Retrieves all expired medicines.")]
        public async Task<IActionResult> GetExpiredMedicaments()
        {
            var result = await _medicineRepository.GetExpiredMedicamentsAsync(User, HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
