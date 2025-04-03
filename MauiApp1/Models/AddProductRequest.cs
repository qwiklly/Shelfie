
namespace MauiApp1.Models
{
    public class AddProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public int CategoryId { get; set; } = 1;
        public DateOnly ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public double Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;
    }
}

