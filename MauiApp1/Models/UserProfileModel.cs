using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class UserProfileModel
    {
        [Required(ErrorMessage = "Имя обязательно")]
        public string Name { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? NewPassword { get; set; }
    }
}
