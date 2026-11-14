using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using ShelfieBackend.Repositories;
using ShelfieBackend.Repositories.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ShelfieBackend.Controllers
{
	[Route("api/application")]
	public class AccountController(IAccount _accountrepo) : ApiControllerBase
	{
		[Authorize(Roles = "Admin")]
		[HttpGet("getUsers")]
		[SwaggerOperation(Summary = "Get all users", Description = "Retrieves a list of all registered users. Admins only.")]
		public async Task<IActionResult> GetUsersAsync()
		{
			var result = await _accountrepo.GetUsersAsync();
			return Ok(result);
		}

		[Authorize]
		[HttpGet("getUser")]
		[SwaggerOperation(Summary = "Get a user by email", Description = "Retrieves the profile of the current user (admins: of any user).")]
		public async Task<IActionResult> GetUserAsync([FromQuery] string email)
		{
			if (!Account.CanManage(User, email))
				return Forbid();

			var result = await _accountrepo.GetUserProfileAsync(email);
			return result != null ? Ok(result) : NotFound("User not found");
		}

		[HttpPost("register")]
		[SwaggerOperation(Summary = "Register a new user", Description = "Creates a new user account")]
		public async Task<IActionResult> RegisterAsync([FromBody] RegisterDTO model)
		{
			var result = await _accountrepo.RegisterAsync(model, User);
			return FromResponse(result);
		}

		[HttpPost("login")]
		[SwaggerOperation(Summary = "Login a user", Description = "Authenticate a user with email and password.")]
		public async Task<IActionResult> LoginAsync([FromBody] LoginDTO model)
		{
			var result = await _accountrepo.LoginAsync(model);
			return result.Flag ? Ok(result) : Unauthorized(result);
		}

		[Authorize]
		[HttpDelete("deleteUser")]
		[SwaggerOperation(Summary = "Delete a user", Description = "Deletes the current user's account (admins: any account).")]
		public async Task<IActionResult> DeleteUserAsync([FromQuery] string email)
		{
			var result = await _accountrepo.DeleteUserAsync(new DeleteUserDTO { Email = email }, User);
			return FromResponse(result);
		}

		[Authorize]
		[HttpPut("updateUser/{email}")]
		[SwaggerOperation(Summary = "Update a user", Description = "Update a user's information, such as name, role, or password.")]
		public async Task<IActionResult> UpdateUserAsync(string email, [FromBody] UpdateUserDTO model)
		{
			var result = await _accountrepo.UpdateUserAsync(email, model, User);
			return FromResponse(result);
		}
	}
}
