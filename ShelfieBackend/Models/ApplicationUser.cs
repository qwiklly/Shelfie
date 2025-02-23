using ShelfieBackend.States;

namespace ShelfieBackend.Models
{
	public class ApplicationUser
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public UserRole Role { get; set; } = UserRole.User;
		public string PasswordHash { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
    }
}
