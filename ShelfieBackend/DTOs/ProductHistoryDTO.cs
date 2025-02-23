namespace ShelfieBackend.DTOs
{
    public class ProductHistoryDTO
    {
        public int Id { get; set; }
        public string Product { get; set; } = string.Empty; 
        public int ProductId { get; set; }
        public DateTime ChangeDate { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public int UserId { get; set; }
    }

}
