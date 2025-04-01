using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
	[Route("api/application")]
	[ApiController]
	public class AccountController(IAccount _accountrepo) : ControllerBase
	{
        [Authorize]
        [HttpGet("getUsers")]
        [SwaggerOperation(
           Summary = "Get all users",
           Description = "Retrieves a list of all registered users."
        )]
        public async Task<IActionResult> GetUsersAsync()
		{
			var result = await _accountrepo.GetUsersAsync();
			return Ok(result);
		}

        [HttpGet("getUser")]
        [SwaggerOperation(
            Summary = "Get a user by email",
            Description = "Retrieves a single user by their email address."
        )]
        public async Task<IActionResult> GetUserAsync(string email)
		{
			var result = await _accountrepo.GetUserAsync(email);
            return result != null ? Ok(result) : NotFound("User not found");
        }

		[HttpPost("register")]
		[SwaggerOperation(
			Summary = "Register a new user",
			Description = "Creates a new user account"
        )]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterDTO model)
        {
            var currentUser = HttpContext.User;

            var result = await _accountrepo.RegisterAsync(model, currentUser);
            return Ok(result);
        }

        [HttpPost("login")]
		[SwaggerOperation(
			Summary = "Login a user",
			Description = "Authenticate a user with email and password."
		)]
		public async Task<IActionResult> LoginAsync(LoginDTO model)
		{
			var result = await _accountrepo.LoginAsync(model);
			return Ok(result);
		}

        [Authorize]
        [HttpDelete("deleteUser")]
		[SwaggerOperation(
			Summary = "Delete a user",
			Description = "Delete a user."
		)]
		public async Task<IActionResult> DeleteUserAsync(DeleteUserDTO model)
		{
			var result = await _accountrepo.DeleteUserAsync(model);
			return Ok(result);
		}

        [Authorize]
        [HttpPut("updateUser/{email}")]
		[SwaggerOperation(
			Summary = "Update a user",
			Description = "Update a user's information, such as name, role, or password."
		)]
		public async Task<IActionResult> UpdateUserAsync(string email, UpdateUserDTO model)
		{
            var currentUser = HttpContext.User;
            var result = await _accountrepo.UpdateUserAsync(email, model, currentUser);
			return Ok(result);
		}
    }
}
