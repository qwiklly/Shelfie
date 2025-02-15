using Microsoft.IdentityModel.Tokens;
using Serilog;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using static ShelfieBackend.Responses.CustomResponses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ShelfieBackend.Data;
using Microsoft.EntityFrameworkCore;
using ShelfieBackend.States;

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
        // Handles user login by verifying credentials and generating a JWT token if successful.
        public async Task<LoginResponse> LoginAsync(LoginDTO model)
        {
            try
            {
                var findUser = await GetUserAsync(model.Email);
                if (findUser == null || string.IsNullOrEmpty(findUser.PasswordHash) || !BCrypt.Net.BCrypt.Verify(model.Password, findUser.PasswordHash))
                    return new LoginResponse(false, "Invalid email or password.");

                // Generates a JWT token for the authenticated user.
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
                var findUser = await GetUserAsync(model.Email);
                if (findUser != null) return new RegisterResponse(false, "User already exists");

                if (model.Password != model.ConfirmPassword)
                    return new RegisterResponse(false, "Passwords do not match");

                // Если роль не указана, то по умолчанию - User
                var newRole = model.Role ?? UserRole.User;

                // Проверка: если пытаются создать админа или работника, но текущий пользователь не админ — запретить
                if ((newRole == UserRole.Admin || newRole == UserRole.Worker) && !currentUser.IsInRole("Admin"))
                {
                    return new RegisterResponse(false, "Only admins can create Admin or Worker accounts.");
                }
               
                var newUser = new ApplicationUser
                {
                    Role = newRole,
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Phone = model.Phone,
                    DateOfBirth = model.DateOfBirth
                };
               
                _appDbContext.Users.Add(newUser);
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
            try
            {
                var users = await _appDbContext.Users
                .AsNoTracking()
                .Select(u => new GetUsersDTO
                {
                    Role = u.Role,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone,
                    DateOfBirth = u.DateOfBirth
                })
                .ToListAsync();

                return users;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while finding users");
                throw new InvalidOperationException("Error while finding users ");
            }
        }
        // Retrieves a single user based on their email address.
        public async Task<ApplicationUser?> GetUserAsync(string email)
            => await _appDbContext.Users.FirstOrDefaultAsync(e => e.Email == email);

        // Updates an existing user's details if found, otherwise returns an error message.
        public async Task<RegisterResponse> UpdateUserAsync(string email, RegisterDTO model, ClaimsPrincipal currentUser)
        {
            try
            {
                var user = await GetUserAsync(email);
                if (user == null)
                {
                    return new RegisterResponse(false, $"User with email '{email}' not found.");
                }

                user.Name = model.Name ?? user.Name;
                user.Phone = model.Phone ?? user.Phone;

                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                // Проверка: менять роль может только админ
                if (model.Role.HasValue && model.Role != user.Role)
                {
                    if (!currentUser.IsInRole("Admin"))
                    {
                        return new RegisterResponse(false, "Only admins can change user roles.");
                    }
                    user.Role = model.Role.Value;
                }

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
        public async Task<BaseResponse> DeleteUserAsync(DeleteUserDTO model)
        {
            try
            {
                var user = await GetUserAsync(model.Email);
                if (user == null)
                    return new BaseResponse(false, "User not found");
                  
                _appDbContext.Users.Remove(user);
                await _appDbContext.SaveChangesAsync();
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
            try
            {
                // Generate security key and credentials.
                var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing in configuration.");
                var jwtIssuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is missing in configuration.");
                var jwtAudience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is missing in configuration.");

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                // Define the user claims to be included in the token.
                var userClaims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Name!),
                    new Claim(ClaimTypes.Email, user.Email!),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                };
                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: userClaims,
                    expires: DateTime.UtcNow.AddDays(2),
                    signingCredentials: credentials
                    );
                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "error while Generating Token");
                throw new Exception("Error while Generating Token ");
            }
        }
    }
}