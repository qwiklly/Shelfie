using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Repositories.Interfaces;
using ShelfieBackend.States;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Repositories
{
    public class Account : IAccount
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly IConfiguration _config;

        public Account(ApplicationDbContext appDbContext, IConfiguration config)
        {
            _appDbContext = appDbContext;
            _config = config;
        }

        internal static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>True when <paramref name="currentUser"/> is the owner of <paramref name="email"/> or an admin.</summary>
        public static bool CanManage(ClaimsPrincipal currentUser, string email)
        {
            if (currentUser.IsInRole(nameof(UserRole.Admin)))
                return true;

            var ownEmail = currentUser.FindFirst(ClaimTypes.Email)?.Value;
            return ownEmail != null && NormalizeEmail(ownEmail) == NormalizeEmail(email);
        }

        // Handles user login by verifying credentials and generating a JWT token if successful.
        public async Task<LoginResponse> LoginAsync(LoginDTO model)
        {
            try
            {
                var findUser = await GetUserAsync(model.Email);
                if (findUser == null || string.IsNullOrEmpty(findUser.PasswordHash) || !BCrypt.Net.BCrypt.Verify(model.Password, findUser.PasswordHash))
                    return new LoginResponse(false, "Invalid email or password.");

                string jwtToken = GenerateToken(findUser);
                return new LoginResponse(true, "Login successfully", jwtToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while signing in");
                return new LoginResponse(false, "Error occurred while signing in");
            }
        }

        // Checks if the user already exists; if not, registers a new user by adding it to the database.
        public async Task<RegisterResponse> RegisterAsync(RegisterDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                var email = NormalizeEmail(model.Email);
                if (await GetUserAsync(email) != null)
                    return new RegisterResponse(false, "User already exists");

                if (model.Password != model.ConfirmPassword)
                    return new RegisterResponse(false, "Passwords do not match");

                if (!PasswordRules.IsStrong(model.Password))
                    return new RegisterResponse(false, PasswordRules.ErrorMessage);

                var newRole = model.Role ?? UserRole.User;

                // Only an admin may create Admin or Worker accounts.
                if (newRole != UserRole.User && !currentUser.IsInRole(nameof(UserRole.Admin)))
                    return new RegisterResponse(false, "Only admins can create Admin or Worker accounts.");

                _appDbContext.Users.Add(new ApplicationUser
                {
                    Role = newRole,
                    Name = model.Name.Trim(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                    DateOfBirth = model.DateOfBirth
                });
                await _appDbContext.SaveChangesAsync();

                return new RegisterResponse(true, "User registered successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while signing up");
                return new RegisterResponse(false, "Error occurred while signing up");
            }
        }

        // Retrieves a list of all users in the system
        public async Task<List<GetUsersDTO>> GetUsersAsync()
        {
            return await _appDbContext.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new GetUsersDTO
                {
                    Role = u.Role,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone,
                    DateOfBirth = u.DateOfBirth
                })
                .ToListAsync();
        }

        // Retrieves a single user (entity, including the password hash — for internal use only).
        public async Task<ApplicationUser?> GetUserAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            // ToLower() on the column keeps accounts created before e-mails were normalized reachable.
            return await _appDbContext.Users.FirstOrDefaultAsync(e => e.Email.ToLower() == normalized);
        }

        public async Task<UserProfileDTO?> GetUserProfileAsync(string email)
        {
            var user = await GetUserAsync(email);
            return user == null ? null : new UserProfileDTO
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Phone = user.Phone,
                DateOfBirth = user.DateOfBirth
            };
        }

        // Updates an existing user's details if found, otherwise returns an error message.
        public async Task<RegisterResponse> UpdateUserAsync(string email, UpdateUserDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                if (!CanManage(currentUser, email))
                    return new RegisterResponse(false, "You can only update your own account.");

                var user = await GetUserAsync(email);
                if (user == null)
                    return new RegisterResponse(false, $"User with email '{email}' not found.");

                // Role is checked before anything is modified so a rejected request changes nothing.
                if (model.Role.HasValue && model.Role != user.Role && !currentUser.IsInRole(nameof(UserRole.Admin)))
                    return new RegisterResponse(false, "Only admins can change user roles.");

                if (!string.IsNullOrEmpty(model.Password) && !PasswordRules.IsStrong(model.Password))
                    return new RegisterResponse(false, PasswordRules.ErrorMessage);

                if (!string.IsNullOrWhiteSpace(model.Name))
                    user.Name = model.Name.Trim();

                if (model.Phone != null)
                    user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

                if (!string.IsNullOrEmpty(model.Password))
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

                if (model.Role.HasValue)
                    user.Role = model.Role.Value;

                await _appDbContext.SaveChangesAsync();
                return new RegisterResponse(true, "User updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating user's data");
                return new RegisterResponse(false, "Error while updating user's data ");
            }
        }

        // Deletes a user by email if they exist, otherwise returns an error message.
        public async Task<BaseResponse> DeleteUserAsync(DeleteUserDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                if (!CanManage(currentUser, model.Email))
                    return new BaseResponse(false, "You can only delete your own account.");

                var user = await GetUserAsync(model.Email);
                if (user == null)
                    return new BaseResponse(false, "User not found");

                await using var transaction = await _appDbContext.Database.BeginTransactionAsync();

                // Values of custom categories are not linked by a foreign key, so remove them explicitly.
                var categoryIds = _appDbContext.Categories.Where(c => c.UserId == user.Id).Select(c => c.Id);
                await _appDbContext.CategoryFieldValues
                    .Where(v => categoryIds.Contains(v.CategoryId))
                    .ExecuteDeleteAsync();

                _appDbContext.Users.Remove(user);
                await _appDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new BaseResponse(true, "User deleted successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while deleting user");
                return new BaseResponse(false, "Error while deleting user ");
            }
        }

        // Generates a JWT token based on the user's details.
        private string GenerateToken(ApplicationUser user)
        {
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing in configuration.");
            var jwtIssuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is missing in configuration.");
            var jwtAudience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is missing in configuration.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var userClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
            };
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: userClaims,
                expires: DateTime.UtcNow.AddDays(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
