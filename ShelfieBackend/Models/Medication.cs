namespace ShelfieBackend.Models
{
    public class Medication
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string? Description { get; set; }
        public double? Weight { get; set; }
        public string WeightUnit { get; set; } = string.Empty;
        public Category Category { get; set; } = null!;
        public DateOnly? ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
        public DateOnly CreatedAt { get; set; }
    }
}
