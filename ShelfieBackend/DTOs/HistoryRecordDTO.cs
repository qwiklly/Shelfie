namespace ShelfieBackend.DTOs
{
    public class HistoryRecordDTO
    {
        public int Id { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public DateTime ChangeDate { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
    }
}
