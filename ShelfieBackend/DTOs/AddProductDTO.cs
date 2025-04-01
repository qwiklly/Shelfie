using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
    public class AddProductDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Creator { get; set; } = string.Empty;
        public int CategoryId { get; set; } = 1 ;
        public DateOnly? ExpirationDate { get; set; }
        public DateOnly? CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [Required]
        public int Quantity { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;

    }
}
