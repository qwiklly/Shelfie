using System.Net;
using System.Net.Http.Json;
using ShelfieBackend.DTOs;
using ShelfieBackend.States;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Responses.CustomResponses;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class AccountTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    [Fact]
    public async Task Register_then_login_returns_a_token()
    {
        var registration = NewRegistration();
        var client = factory.CreateClient();

        var register = await client.RegisterAsync(registration);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.True((await register.ReadAsync<BaseResponse>()).Flag);

        var login = await client.LoginAsync(registration.Email, registration.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.ReadAsync<LoginResponse>();
        Assert.True(body.Flag);
        Assert.False(string.IsNullOrEmpty(body.JWTToken));
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected_with_401()
    {
        var registration = NewRegistration();
        var client = factory.CreateClient();
        await client.RegisterAsync(registration);

        var login = await client.LoginAsync(registration.Email, "Wrong123");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.False((await login.ReadAsync<LoginResponse>()).Flag);
    }

    [Fact]
    public async Task Email_is_case_insensitive_for_login_and_duplicates()
    {
        var email = UniqueEmail("MixedCase");
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.RegisterAsync(NewRegistration(email))).StatusCode);

        var duplicate = await client.RegisterAsync(NewRegistration(email.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var login = await client.LoginAsync("  " + email.ToUpperInvariant() + " ", DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("Abc123")]
    [InlineData("Secret123")]
    public async Task Strong_password_is_accepted(string password)
    {
        var response = await factory.CreateClient().RegisterAsync(NewRegistration(password: password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("alllowercase1")]
    [InlineData("NoDigitsHere")]
    [InlineData("ALLUPPER123")]
    public async Task Weak_password_is_rejected(string password)
    {
        var response = await factory.CreateClient().RegisterAsync(NewRegistration(password: password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_does_not_apply_the_password_complexity_rule()
    {
        // Old accounts may have passwords that do not match today's rule; they must still be able to log in.
        var email = UniqueEmail("legacy");
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new Models.ApplicationUser
            {
                Name = "Legacy",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("weakpass"),
                DateOfBirth = new DateOnly(1999, 1, 1)
            });
            return await db.SaveChangesAsync(CT);
        });

        var login = await factory.CreateClient().LoginAsync(email, "weakpass");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Anonymous_user_cannot_register_an_admin()
    {
        var registration = NewRegistration();
        registration.Role = UserRole.Admin;

        var response = await factory.CreateClient().RegisterAsync(registration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var user = await factory.WithDbAsync(db => Task.FromResult(db.Users.FirstOrDefault(u => u.Email == registration.Email)));
        Assert.Null(user);
    }

    [Fact]
    public async Task Admin_can_register_a_worker()
    {
        var admin = await factory.LoginAsAdminAsync();
        var registration = NewRegistration();
        registration.Role = UserRole.Worker;

        var response = await admin.RegisterAsync(registration);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUser_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync($"api/application/getUser?email={ShelfieApiFactory.AdminEmail}", CT);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUser_returns_own_profile_without_password_hash()
    {
        var (client, email) = await factory.CreateUserAsync();

        var response = await client.GetAsync($"api/application/getUser?email={Uri.EscapeDataString(email)}", CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(CT);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2", json); // bcrypt hash prefix
        var profile = await response.ReadAsync<UserProfileDTO>();
        Assert.Equal(email, profile.Email);
    }

    [Fact]
    public async Task User_cannot_read_profile_of_another_user()
    {
        var (client, _) = await factory.CreateUserAsync();
        var (_, otherEmail) = await factory.CreateUserAsync();

        var response = await client.GetAsync($"api/application/getUser?email={Uri.EscapeDataString(otherEmail)}", CT);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_is_admin_only()
    {
        var (client, _) = await factory.CreateUserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/application/getUsers", CT)).StatusCode);

        var admin = await factory.LoginAsAdminAsync();
        var users = await admin.GetJsonAsync<List<GetUsersDTO>>("api/application/getUsers");
        Assert.Contains(users, u => u.Email == ShelfieApiFactory.AdminEmail);
    }

    [Fact]
    public async Task User_cannot_update_another_users_account()
    {
        var (attacker, _) = await factory.CreateUserAsync();
        var (_, victimEmail) = await factory.CreateUserAsync();

        var response = await attacker.PutAsJsonAsync($"api/application/updateUser/{victimEmail}",
            new UpdateUserDTO { Name = "Hacked", Password = "Hacked123" }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // The victim can still log in with the original password.
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(victimEmail, DefaultPassword)).StatusCode);
    }

    [Fact]
    public async Task User_can_update_own_name_phone_and_password()
    {
        var (client, email) = await factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync($"api/application/updateUser/{email}",
            new UpdateUserDTO { Name = "Renamed", Phone = "+15550001111", Password = "NewSecret1" }, CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await client.GetJsonAsync<UserProfileDTO>($"api/application/getUser?email={Uri.EscapeDataString(email)}");
        Assert.Equal("Renamed", profile.Name);
        Assert.Equal("+15550001111", profile.Phone);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(email, "NewSecret1")).StatusCode);
    }

    [Fact]
    public async Task Rejected_role_change_does_not_save_other_fields()
    {
        var (client, email) = await factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync($"api/application/updateUser/{email}",
            new UpdateUserDTO { Name = "Should not stick", Role = UserRole.Admin }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var profile = await client.GetJsonAsync<UserProfileDTO>($"api/application/getUser?email={Uri.EscapeDataString(email)}");
        Assert.Equal(UserRole.User, profile.Role);
        Assert.Equal("Test User", profile.Name);
    }

    [Fact]
    public async Task Update_with_weak_password_is_rejected()
    {
        var (client, email) = await factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync($"api/application/updateUser/{email}", new UpdateUserDTO { Password = "123" }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_delete_another_users_account()
    {
        var (attacker, _) = await factory.CreateUserAsync();
        var (_, victimEmail) = await factory.CreateUserAsync();

        var response = await attacker.DeleteAsync($"api/application/deleteUser?email={Uri.EscapeDataString(victimEmail)}", CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(victimEmail, DefaultPassword)).StatusCode);
    }

    [Fact]
    public async Task User_can_delete_own_account_with_all_data()
    {
        var (client, email) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/category/createCategory", new AddCategoryDTO { Name = "Books" }, CT);
        var category = (await client.GetJsonAsync<List<GetCategoriesDTO>>("api/category/getAllCategories")).Single();
        await client.PostAsJsonAsync($"api/category/{category.Id}/addOrChangeFields", new[] { "Title" }, CT);
        await client.PostAsJsonAsync($"api/categoryValue/{category.Id}/save-values",
            new Models.FieldValuesRequest { FieldValues = new() { [1] = "Dune" } }, CT);

        var response = await client.DeleteAsync($"api/application/deleteUser?email={Uri.EscapeDataString(email)}", CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().LoginAsync(email, DefaultPassword)).StatusCode);
        var leftovers = await factory.WithDbAsync(db => Task.FromResult(db.CategoryFieldValues.Count(v => v.CategoryId == category.Id)));
        Assert.Equal(0, leftovers);
    }
}
