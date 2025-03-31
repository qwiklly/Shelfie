using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Pages
{
	public class UsersPageModel : PageModel
	{
		private readonly IAccount _applicationService;

		public List<GetUsersDTO> Users { get; set; } = new List<GetUsersDTO>();
		public DeleteUserDTO Delete = new();

		public UsersPageModel(IAccount applicationService)

		{
			_applicationService = applicationService;
		}

		public async Task OnGetAsync()
		{

			TempData["SuccessMessage"] = null;
			TempData["ErrorMessage"] = null;
			Users = await _applicationService.GetUsersAsync();
		}

		public async Task<IActionResult> OnPostDeleteAsync(string email)
		{
			if (string.IsNullOrEmpty(email))
			{
				return BadRequest("Email is required.");
			}

			Delete.Email = email;

			var response = await _applicationService.DeleteUserAsync(Delete);
			if (response.Flag == true)
			{

				return RedirectToPage();
			}
			else
			{
				ModelState.AddModelError(string.Empty, response.Message ?? "Не удалось удалить пользователя.");
				return Page();
			}
		}
	}
}


