namespace MauiApp1.Models
{
    public class FieldValuesRequest
    {
        public int RecordId { get; set; }
        public Dictionary<int, string> FieldValues { get; set; } = new();
    }
}
