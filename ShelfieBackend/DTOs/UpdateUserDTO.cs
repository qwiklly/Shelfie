using ShelfieBackend.States;

namespace ShelfieBackend.DTOs
{
    public class UpdateUserDTO
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public UserRole? Role { get; set; }
    }
}
