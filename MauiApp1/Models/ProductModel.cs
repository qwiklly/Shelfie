namespace MauiApp1.Models
{
    public class ProductModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public DateOnly ExpirationDate { get; set; } 
        public int CategoryId { get; set; }
        public DateOnly CreatedAt { get; set; }
        public int Quantity { get; set; }
        public double Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;
    }
}


