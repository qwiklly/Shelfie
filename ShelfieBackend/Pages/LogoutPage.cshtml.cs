using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShelfieBackend.States;

namespace ShelfieBackend.Pages
{
    public class LogoutPageModel(AuthenticationStateProvider authStateProvider) : PageModel
    {
		private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;

		public async Task<IActionResult> OnGet()
        {
            var customAuthStateProvider = (AuthenticationProvider)_authStateProvider;
            await customAuthStateProvider.UpdateAuthenticationState(null);
            return RedirectToPage("/Login");
        }	
	}
}
