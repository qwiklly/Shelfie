﻿namespace ShelfieBackend.Models
{
    public class CustomItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
}
