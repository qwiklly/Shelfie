namespace ShelfieBackend.DTOs
{
    /// <summary>A field of a custom category. FieldOrder is the key used in record values.</summary>
    public class CategoryFieldDTO
    {
        public int FieldOrder { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
