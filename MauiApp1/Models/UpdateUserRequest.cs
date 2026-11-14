using System.Text.Json.Serialization;

namespace MauiApp1.Models
{
    public class UpdateUserRequest
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Password { get; set; }
    }
}
