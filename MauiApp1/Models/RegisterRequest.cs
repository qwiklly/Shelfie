namespace MauiApp1.Models
{
	public class RegisterRequest
	{
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string ConfirmPassword { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Role { get; set; }
        public string Phone { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
    }
}
