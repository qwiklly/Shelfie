﻿namespace ShelfieBackend.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public ProductCategory Category { get; set; } = null!;
        public DateOnly? ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
        public DateOnly CreatedAt { get; set; }
    }
}
