namespace MauiApp1.Models
{
    class AddRecordRequest
    {
        public int RecordId { get; set; } 
        public Dictionary<int, string> FieldValues { get; set; } = new ();
    }
}
