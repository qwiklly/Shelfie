using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
    public class UpdateProductDTO
    {
        [Required]
        public string? Name { get; set; }
        public string? Creator { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public int? Quantity { get; set; }
        public double Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;
    }
}
