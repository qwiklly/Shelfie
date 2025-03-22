namespace ShelfieBackend.Models
{
    public class CategoryFieldValue
    {
        public int Id { get; set; }
        public int RecordId { get; set; }
        public int CategoryFieldId { get; set; }
        public int CategoryId { get; set; }
        public string Value { get; set; } = string.Empty;  
    }
}
