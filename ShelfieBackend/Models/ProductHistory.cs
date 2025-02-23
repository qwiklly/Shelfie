namespace ShelfieBackend.Models
{
    public class ProductHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public DateTime ChangeDate { get; set; }  
        public string ChangeType { get; set; } = string.Empty; // "Added", "Removed", "Updated"
        public int QuantityChange { get; set; } 
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
}
