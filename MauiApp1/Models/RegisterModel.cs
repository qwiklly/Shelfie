using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class RegisterModel
    {
        [Required]
        public string RegisterName { get; set; } = "";

        [Required]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string RegisterEmail { get; set; } = "";

        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Неверный формат телефона")]
        public string RegisterPhone { get; set; } = "";

        [Required]
        public DateTime RegisterBirthDate { get; set; } = DateTime.Today;

        [Required]
        [RegularExpression("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9]).{6,}$",
            ErrorMessage = "Пароль должен содержать заглавную, строчную букву, цифру и быть более 5 символов")]
        public string RegisterPassword { get; set; } = "";

        [Required]
        [Compare("RegisterPassword", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmRegisterPassword { get; set; } = "";
    }
}