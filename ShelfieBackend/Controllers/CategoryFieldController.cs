using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShelfieBackend.Data;
using ShelfieBackend.Models;
using System;

namespace ShelfieBackend.Controllers
{
    [ApiController]
    [Route("api/category/{categoryId:int}/fields")]
    public class CategoryFieldsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoryFieldsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Получить список полей для категории
        [HttpGet]
        public async Task<IActionResult> GetCategoryFields(int categoryId)
        {
            var fields = await _context.CategoryFields
                .Where(f => f.CategoryId == categoryId)
                .Select(f => f.FieldName)
                .ToListAsync();

            return Ok(fields);
        }

        // Сохранить или обновить поля категории
        [HttpPost]
        public async Task<IActionResult> SaveCategoryFields(int categoryId, [FromBody] List<string> fieldNames)
        {
            if (fieldNames == null || !fieldNames.Any())
            {
                return BadRequest("Необходимо передать хотя бы одно поле.");
            }

            // Удаляем старые поля
            var existingFields = _context.CategoryFields.Where(f => f.CategoryId == categoryId);
            _context.CategoryFields.RemoveRange(existingFields);

            // Создаём новые
            var newFields = fieldNames.Select(name => new CategoryField
            {
                CategoryId = categoryId,
                FieldName = name
            }).ToList();

            await _context.CategoryFields.AddRangeAsync(newFields);
            await _context.SaveChangesAsync();

            return Ok("Поля сохранены.");
        }
    }

}
