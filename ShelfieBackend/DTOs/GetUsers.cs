using ShelfieBackend.States;
using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
	public class GetUsersDTO
	{

		[Required, DataType(DataType.EmailAddress), EmailAddress]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string Name { get; set; } = string.Empty;

		[Required]
		public UserRole Role { get; set; }

        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required, DataType(DataType.Date)]
        public DateOnly DateOfBirth { get; set; }
    }
}
