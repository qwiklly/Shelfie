namespace MauiApp1.Models
{
    public class UpdateProductRequest
    {
        public string? Name { get; set; }
        public string? Creator { get; set; }
        public int? CategoryId { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public int? Quantity { get; set; }
        public double? Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;
    }
}
