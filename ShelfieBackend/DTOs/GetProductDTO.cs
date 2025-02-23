
namespace ShelfieBackend.DTOs
{
    public class GetProductDTO
    {
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public DateOnly CreatedAt { get; set; }
    }
}

