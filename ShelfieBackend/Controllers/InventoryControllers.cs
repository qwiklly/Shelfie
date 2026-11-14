using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shelfie.Shared;
using ShelfieBackend.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Text;

namespace ShelfieBackend.Controllers
{
    [Authorize]
    [Route("api/categories")]
    public class CategoriesController(InventoryService inventory) : ApiControllerBase
    {
        [HttpGet]
        [SwaggerOperation(Summary = "All categories", Description = "Built-in categories (Products, Medications, Books) plus the user's own.")]
        public async Task<ActionResult<List<CategoryDto>>> GetAll() =>
            await inventory.GetCategoriesAsync(CurrentUserId, HttpContext.RequestAborted);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var category = await inventory.GetCategoryAsync(CurrentUserId, id, HttpContext.RequestAborted);
            return category != null ? Ok(category) : NotFound();
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Create a category", Description = "Fields have a stable key, a type (Text, Number, Date, Boolean) and may be required.")]
        public async Task<IActionResult> Create([FromBody] CategoryDto dto)
        {
            if (dto.Id != Guid.Empty && await inventory.GetCategoryAsync(CurrentUserId, dto.Id, HttpContext.RequestAborted) != null)
                return BadRequest(new ApiMessage(false, "Категория с таким id уже существует"));
            return FromResult(await inventory.SaveCategoryAsync(CurrentUserId, dto, HttpContext.RequestAborted));
        }

        [HttpPut("{id:guid}")]
        [SwaggerOperation(Summary = "Update a category", Description = "Rename it or change its fields. Built-in categories can be customised too.")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CategoryDto dto)
        {
            if (await inventory.GetCategoryAsync(CurrentUserId, id, HttpContext.RequestAborted) == null)
                return NotFound();
            dto.Id = id;
            return FromResult(await inventory.SaveCategoryAsync(CurrentUserId, dto, HttpContext.RequestAborted));
        }

        [HttpDelete("{id:guid}")]
        [SwaggerOperation(Summary = "Delete a category", Description = "Deletes a custom category together with its items.")]
        public async Task<IActionResult> Delete(Guid id) =>
            FromResult(await inventory.DeleteCategoryAsync(CurrentUserId, id, HttpContext.RequestAborted));
    }

    [Authorize]
    [Route("api/items")]
    public class ItemsController(InventoryService inventory) : ApiControllerBase
    {
        [HttpGet]
        [SwaggerOperation(Summary = "Items", Description = "Optional filters: categoryId, search (name, brand, barcode, fields), location, expiringWithinDays.")]
        public async Task<ActionResult<List<ItemDto>>> GetAll(
            [FromQuery] Guid? categoryId, [FromQuery] string? search, [FromQuery] string? location, [FromQuery] int? expiringWithinDays) =>
            await inventory.GetItemsAsync(CurrentUserId, new ItemQuery(categoryId, search, location, expiringWithinDays), HttpContext.RequestAborted);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var item = await inventory.GetItemAsync(CurrentUserId, id, HttpContext.RequestAborted);
            return item != null ? Ok(item) : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ItemDto dto) =>
            FromResult(await inventory.CreateItemAsync(CurrentUserId, dto, HttpContext.RequestAborted));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ItemDto dto) =>
            FromResult(await inventory.UpdateItemAsync(CurrentUserId, id, dto, HttpContext.RequestAborted));

        [HttpDelete("{id:guid}")]
        [SwaggerOperation(Summary = "Delete an item", Description = "reason: Removed (default), Consumed or Wasted — used for statistics.")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] string? reason) =>
            FromResult(await inventory.DeleteItemAsync(CurrentUserId, id, reason, HttpContext.RequestAborted));

        public sealed record AdjustRequest(int Delta);

        [HttpPost("{id:guid}/adjust")]
        [SwaggerOperation(Summary = "Change quantity by delta", Description = "Reaching zero adds the item to the shopping list.")]
        public async Task<IActionResult> Adjust(Guid id, [FromBody] AdjustRequest request) =>
            FromResult(await inventory.AdjustQuantityAsync(CurrentUserId, id, request.Delta, HttpContext.RequestAborted));

        [HttpGet("export")]
        [SwaggerOperation(Summary = "Export to CSV", Description = "Semicolon-separated, UTF-8 with BOM (opens in Excel).")]
        public async Task<IActionResult> Export()
        {
            var csv = await inventory.ExportCsvAsync(CurrentUserId, HttpContext.RequestAborted);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"shelfie-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }
    }

    [Authorize]
    [Route("api/shopping")]
    public class ShoppingController(InventoryService inventory) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ShoppingItemDto>>> GetAll() =>
            await inventory.GetShoppingListAsync(CurrentUserId, HttpContext.RequestAborted);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ShoppingItemDto dto) =>
            FromResult(await inventory.SaveShoppingItemAsync(CurrentUserId, null, dto, HttpContext.RequestAborted));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ShoppingItemDto dto) =>
            FromResult(await inventory.SaveShoppingItemAsync(CurrentUserId, id, dto, HttpContext.RequestAborted));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id) =>
            FromResult(await inventory.DeleteShoppingItemAsync(CurrentUserId, id, HttpContext.RequestAborted));
    }

    [Authorize]
    [Route("api/history")]
    public class HistoryController(InventoryService inventory) : ApiControllerBase
    {
        [HttpGet]
        [SwaggerOperation(Summary = "History", Description = "Newest first.")]
        public async Task<ActionResult<List<HistoryEntryDto>>> Get([FromQuery] int take = 200) =>
            await inventory.GetHistoryAsync(CurrentUserId, take, HttpContext.RequestAborted);

        [HttpDelete]
        [SwaggerOperation(Summary = "Clear the history of the current user")]
        public async Task<IActionResult> Clear()
        {
            await inventory.ClearHistoryAsync(CurrentUserId, HttpContext.RequestAborted);
            return Ok(new ApiMessage(true, "История очищена"));
        }
    }

    [Authorize]
    [Route("api/sync")]
    public class SyncController(InventoryService inventory) : ApiControllerBase
    {
        [HttpPost]
        [SwaggerOperation(
            Summary = "Synchronise a device",
            Description = "Send local changes and the cursor from the previous sync; receive server changes since then and a new cursor. Last write (UpdatedAt) wins.")]
        public async Task<ActionResult<SyncResponse>> Sync([FromBody] SyncRequest request) =>
            await inventory.SyncAsync(CurrentUserId, request, HttpContext.RequestAborted);
    }
}
