using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
	public class LoginDTO
	{
		[Required, DataType(DataType.EmailAddress), EmailAddress]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[Required, DataType(DataType.Password)]
		[RegularExpression("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[a-z]).{6,}$",
			ErrorMessage = "Ваш пароль должен содержать комбинацию прописных, строчних букв и цифр и быть длиной более 5 символов ")]

		public string Password { get; set; } = string.Empty;
	}
}
