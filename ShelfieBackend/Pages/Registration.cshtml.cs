using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShelfieBackend.DTOs;
using ShelfieBackend.Services;

namespace ShelfieBackend.Pages
{
    public class RegistrationModel : PageModel
    {
        [BindProperty]
        public RegisterDTO Register { get; set; } = new RegisterDTO();

        public bool IsUserAdded { get; set; } = false;

        private readonly IAccountService _accountService;

        public RegistrationModel(IAccountService accountService)
        {
            _accountService = accountService;
        }

        public void OnGet()
        {
            // clear temp after reloading page
            TempData["SuccessMessage"] = null;
            TempData["ErrorMessage"] = null;
        }

        public async Task<IActionResult> OnPostAsync()
        {

			if (!ModelState.IsValid)
            {
                return Page();
            }
			var response = await _accountService.RegisterAsync(Register);

            if (!response.Flag)
            {
                TempData["ErrorMessage"] = response.Message;
                return Page();
            }

            IsUserAdded = true;  
            return Page(); 
        }
    }
}
