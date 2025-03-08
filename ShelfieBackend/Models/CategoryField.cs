namespace ShelfieBackend.Models
{
    public class CategoryField
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string FieldName { get; set; } = string.Empty;
    }
}
