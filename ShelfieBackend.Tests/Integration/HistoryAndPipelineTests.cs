using System.Net;
using System.Net.Http.Json;
using ShelfieBackend.DTOs;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class HistoryAndPipelineTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private static AddProductDTO Product(string name) => new() { Name = name, Creator = "Test", Quantity = 1 };

    [Fact]
    public async Task Clearing_history_only_affects_the_current_user()
    {
        // Regression: DELETE api/application/clear used to wipe the history of every user.
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        await alice.PostAsJsonAsync("api/application/add", Product("Tea"), CT);
        await bob.PostAsJsonAsync("api/application/add", Product("Coffee"), CT);

        var clear = await alice.DeleteAsync("api/application/clear", CT);

        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Empty(await alice.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory"));
        Assert.Single(await bob.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory"));
    }

    [Fact]
    public async Task History_is_sorted_newest_first()
    {
        var (client, _) = await factory.CreateUserAsync();
        foreach (var name in new[] { "First", "Second", "Third" })
            await client.PostAsJsonAsync("api/application/add", Product(name), CT);

        var history = await client.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory");

        Assert.Equal(["Third", "Second", "First"], history.Select(h => h.ItemName));
    }

    [Fact]
    public async Task Unauthorized_api_response_is_problem_json()
    {
        var response = await factory.CreateClient().GetAsync("api/application/getAllHistory", CT);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.WwwAuthenticate.Count > 0, "WWW-Authenticate header must be kept");
    }

    [Fact]
    public async Task Unknown_route_is_404_problem_json()
    {
        var response = await factory.CreateClient().GetAsync("api/does-not-exist", CT);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Validation_errors_keep_their_details()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("api/application/login", new { email = "not-an-email" }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(CT);
        Assert.Contains("Password", body);
    }

    [Fact]
    public async Task Admin_pages_redirect_anonymous_users_to_login()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/UsersPage", CT);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Swagger_document_is_served()
    {
        var response = await factory.CreateClient().GetAsync("swagger/v1/swagger.json", CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/category/{categoryId}/fields", await response.Content.ReadAsStringAsync(CT));
    }
}
