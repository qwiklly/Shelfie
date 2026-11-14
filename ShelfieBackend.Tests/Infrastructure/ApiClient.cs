using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ShelfieBackend.DTOs;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Tests.Infrastructure;

/// <summary>Helpers for talking to the in-memory API as a particular user.</summary>
public static class ApiClient
{
    public static readonly JsonSerializerOptions Json = Shelfie.Shared.ShelfieJson.Options;

    public const string DefaultPassword = "Secret123";

    public static CancellationToken CT => TestContext.Current.CancellationToken;

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@shelfie.test";

    public static RegisterDTO NewRegistration(string? email = null, string password = DefaultPassword) => new()
    {
        Email = email ?? UniqueEmail(),
        Password = password,
        ConfirmPassword = password,
        Name = "Test User",
        Phone = "+79991234567",
        DateOfBirth = new DateOnly(2000, 5, 17)
    };

    public static async Task<HttpResponseMessage> RegisterAsync(this HttpClient client, RegisterDTO model) =>
        await client.PostAsJsonAsync("api/application/register", model, CT);

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("api/application/login", new LoginDTO { Email = email, Password = password }, CT);

    /// <summary>Logs in and returns a client that sends the bearer token.</summary>
    public static async Task<HttpClient> LoginAsAsync(this ShelfieApiFactory factory, string email, string password)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, CT);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrEmpty(login.JWTToken));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.JWTToken);
        return client;
    }

    /// <summary>Registers a fresh user and returns an authenticated client plus the e-mail.</summary>
    public static async Task<(HttpClient Client, string Email)> CreateUserAsync(this ShelfieApiFactory factory)
    {
        var registration = NewRegistration();
        var response = await factory.CreateClient().RegisterAsync(registration);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await factory.LoginAsAsync(registration.Email, registration.Password), registration.Email);
    }

    public static Task<HttpClient> LoginAsAdminAsync(this ShelfieApiFactory factory) =>
        factory.LoginAsAsync(ShelfieApiFactory.AdminEmail, ShelfieApiFactory.AdminPassword);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, CT);
        Assert.NotNull(value);
        return value;
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url, CT);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<T>();
    }
}
