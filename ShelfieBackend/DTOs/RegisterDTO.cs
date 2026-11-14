using ShelfieBackend.States;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ShelfieBackend.DTOs
{
	public class RegisterDTO : LoginDTO, IValidatableObject
	{
		[Required, Compare(nameof(Password)), DataType(DataType.Password)]
		public string ConfirmPassword { get; set; } = string.Empty;

		[Required]
		public string Name { get; set; } = string.Empty;

		public UserRole? Role { get; set; }

		[RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "The phone number is not valid.")]
		public string? Phone { get; set; }

		[Required, DataType(DataType.Date)]
		public DateOnly DateOfBirth { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (!PasswordRules.IsStrong(Password))
				yield return new ValidationResult(PasswordRules.ErrorMessage, [nameof(Password)]);
		}
	}

	public static class PasswordRules
	{
		// Upper-case letter, lower-case letter and a digit, at least 6 characters.
		private static readonly Regex Pattern = new("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9]).{6,}$", RegexOptions.Compiled);

		public const string ErrorMessage = "Пароль должен содержать заглавную и строчную букву, цифру и быть не короче 6 символов";

		public static bool IsStrong(string? password) => password != null && Pattern.IsMatch(password);
	}
}
