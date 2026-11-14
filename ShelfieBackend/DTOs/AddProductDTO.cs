using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
    public class AddProductDTO
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Creator { get; set; } = string.Empty;

        /// <summary>0 or an unknown id falls back to the default category of the item type.</summary>
        public int CategoryId { get; set; }

        public DateOnly? ExpirationDate { get; set; }

        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public double Weight { get; set; }

        public string WeightUnit { get; set; } = string.Empty;
    }
}
