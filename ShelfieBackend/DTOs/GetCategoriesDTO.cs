using ShelfieBackend.Models;

namespace ShelfieBackend.DTOs
{
    public class GetCategoriesDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Book", "Product", "Medication", "Custom"
        public int? UserId { get; set; } // Null если это предопределённая категория
    }
}
