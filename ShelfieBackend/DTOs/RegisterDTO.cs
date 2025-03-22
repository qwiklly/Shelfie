using ShelfieBackend.States;
using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
	public class RegisterDTO : LoginDTO
	{
		[Required, Compare(nameof(Password)), DataType(DataType.Password)]
		public string ConfirmPassword { get; set; } = string.Empty;

		[Required]
		public string Name { get; set; } = string.Empty;

		[Required]
		public UserRole? Role { get; set; }

        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "The phone number is not valid.")]
        public string? Phone { get; set; } 

        [Required, DataType(DataType.Date)]
        public DateOnly DateOfBirth { get; set; }
    }
}
