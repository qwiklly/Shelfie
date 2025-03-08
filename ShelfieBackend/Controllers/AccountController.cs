using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.DTOs;
using static ShelfieBackend.Responses.CustomResponses;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using ShelfieBackend.Repositories.Interfaces;

namespace ShelfieBackend.Controllers
{
	[Route("api/application")]
	[ApiController]
	public class AccountController(IAccount _accountrepo) : ControllerBase
	{
		/// <summary>
		/// Get a list of all users.
		/// </summary>
		/// <remarks>
		/// This method retrieves all user records from the database.
		/// </remarks>
		/// <returns>List of all users from the database.</returns>
		[Authorize]
		[HttpGet("getUsers")]
		public async Task<ActionResult<BaseResponse>> GetUsersAsync()
		{
			var result = await _accountrepo.GetUsersAsync();
			return Ok(result);
		}

        /// <summary>
        /// Get one user by id.
        /// </summary>
        /// <remarks>
        /// Retrieve a single user from the database using their email address.
        /// </remarks>
        /// <returns>Return one user.</returns>
        [HttpGet("getUser")]
		public async Task<ActionResult<BaseResponse>> GetUserAsync(string email)
		{
			var result = await _accountrepo.GetUserAsync(email);
			return Ok(result);
		}

		[HttpPost("register")]
		[SwaggerOperation(
			Summary = "Register a new user",
			Description = "Registers a new user."
		)]
        public async Task<ActionResult<RegisterResponse>> RegisterAsync([FromBody] RegisterDTO model)
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
		public async Task<ActionResult<LoginResponse>> LoginAsync(LoginDTO model)
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
		public async Task<ActionResult<BaseResponse>> DeleteUserAsync(DeleteUserDTO model)
		{
			var result = await _accountrepo.DeleteUserAsync(model);
			return Ok(result);
		}

		[HttpPut("updateUser/{email}")]
		[SwaggerOperation(
			Summary = "Update a user",
			Description = "Update a user's information, such as name, role, or password."
		)]
		public async Task<ActionResult<BaseResponse>> UpdateUserAsync(string email, RegisterDTO model)
		{
            var currentUser = HttpContext.User;
            var result = await _accountrepo.UpdateUserAsync(email, model, currentUser);
			return Ok(result);
		}
    }
}
