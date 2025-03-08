namespace ShelfieBackend.DTOs
{
    public class addCategoryDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Book", "Product", "Medication", "Custom"
    }
}
