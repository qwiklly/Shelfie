using System.Text.Json.Serialization;

namespace MauiApp1.Models
{
	public class LoginResponse
	{
		[JsonPropertyName("flag")]
		public bool Flag { get; set; }

		[JsonPropertyName("message")]
		public string Message { get; set; } = string.Empty;

		[JsonPropertyName("jwtToken")]
		public string JWTToken { get; set; } = string.Empty;
	}
}
