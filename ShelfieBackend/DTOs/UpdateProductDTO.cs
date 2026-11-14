namespace ShelfieBackend.DTOs
{
    /// <summary>Partial update: null (or 0 weight / empty unit) means "keep the current value".</summary>
    public class UpdateProductDTO
    {
        public string? Name { get; set; }
        public string? Creator { get; set; }
        public string? Description { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public int? Quantity { get; set; }
        public double? Weight { get; set; }
        public string? WeightUnit { get; set; }
    }
}
