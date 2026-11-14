using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
	public class LoginDTO
	{
		[Required, DataType(DataType.EmailAddress), EmailAddress]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		// No complexity rule on login: it only matters when a password is created.
		[Required, DataType(DataType.Password)]
		public string Password { get; set; } = string.Empty;
	}
}
