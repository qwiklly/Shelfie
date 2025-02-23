using ShelfieBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
    public class AddProductDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        public DateOnly? ExpirationDate { get; set; }
        public DateOnly? CreatedAt { get; set; }

        [Required]
        public int Quantity { get; set; }
    }
}
