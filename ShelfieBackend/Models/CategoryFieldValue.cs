namespace ShelfieBackend.Models
{
    public class CategoryFieldValue
    {
        public int Id { get; set; }
        public int CategoryFieldId { get; set; } 
        public string Value { get; set; } = string.Empty;  
    }
}
