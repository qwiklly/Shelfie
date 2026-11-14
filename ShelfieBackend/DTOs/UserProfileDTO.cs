using ShelfieBackend.States;

namespace ShelfieBackend.DTOs
{
    /// <summary>Public view of a user (never exposes the password hash).</summary>
    public class UserProfileDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string? Phone { get; set; }
        public DateOnly DateOfBirth { get; set; }
    }
}
