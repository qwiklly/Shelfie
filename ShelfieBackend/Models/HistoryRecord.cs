namespace ShelfieBackend.Models
{
    public class HistoryRecord
    {
        public int Id { get; set; }
        public string ItemType { get; set; } = string.Empty; // "Book", "Product", "Medication", "Custom"
        public string ItemName { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ChangeType { get; set; } = string.Empty; // "Added", "Removed", "Updated"
        public int QuantityChange { get; set; }
        public DateTime ChangeDate { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
}
