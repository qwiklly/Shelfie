using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Pages
{
    [Authorize(Roles = "Admin")]
    public class AddUserModel(IAccount account) : PageModel
    {
        [BindProperty]
        public RegisterDTO Register { get; set; } = new RegisterDTO();

        public bool IsUserAdded { get; set; } = false;

        public void OnGet()
        {
            TempData["SuccessMessage"] = null;
            TempData["ErrorMessage"] = null;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // The admin's own principal is passed so Admin/Worker accounts can be created.
            var response = await account.RegisterAsync(Register, User);

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
