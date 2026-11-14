using ShelfieBackend.States;
using System.ComponentModel.DataAnnotations;

namespace ShelfieBackend.DTOs
{
    public class UpdateUserDTO
    {
        public string? Name { get; set; }

        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "The phone number is not valid.")]
        public string? Phone { get; set; }

        public string? Password { get; set; }
        public UserRole? Role { get; set; }
    }
}
