using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ShelfieBackend.DTOs;
using ShelfieBackend.Middleware;
using ShelfieBackend.Repositories;
using ShelfieBackend.Services;

namespace ShelfieBackend.Tests.Unit;

public class PasswordRulesTests
{
    [Theory]
    [InlineData("Abc123", true)]
    [InlineData("Secret123", true)]
    [InlineData("abc123", false)]   // no upper-case letter
    [InlineData("ABC123", false)]   // no lower-case letter
    [InlineData("Abcdef", false)]   // no digit (the old regex accepted this)
    [InlineData("Ab1", false)]      // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStrong(string? password, bool expected) =>
        Assert.Equal(expected, PasswordRules.IsStrong(password));
}

public class AccountRulesTests
{
    private static ClaimsPrincipal Principal(string email, string role = "User") =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "Test"));

    [Fact]
    public void User_can_manage_own_account_case_insensitively() =>
        Assert.True(Account.CanManage(Principal("me@x.io"), " ME@x.io "));

    [Fact]
    public void User_cannot_manage_someone_else() =>
        Assert.False(Account.CanManage(Principal("me@x.io"), "you@x.io"));

    [Fact]
    public void Admin_can_manage_anyone() =>
        Assert.True(Account.CanManage(Principal("admin@x.io", "Admin"), "you@x.io"));

    [Fact]
    public void Anonymous_cannot_manage_anyone() =>
        Assert.False(Account.CanManage(new ClaimsPrincipal(new ClaimsIdentity()), "you@x.io"));
}

public class UserIdServiceTests
{
    private readonly UserIdService _service = new();

    [Theory]
    [InlineData("42", 42)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("abc", null)]
    public void Parses_positive_ids_only(string claim, int? expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, claim)]));
        Assert.Equal(expected, _service.GetUserId(user));
    }

    [Fact]
    public void Missing_claim_gives_null() =>
        Assert.Null(_service.GetUserId(new ClaimsPrincipal()));
}

public class GlobalExceptionMiddlewareTests
{
    private static async Task<(HttpContext Context, string Body)> RunAsync(RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new GlobalException(next).InvokeAsync(context);

        context.Response.Body.Position = 0;
        return (context, await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Leaves_successful_empty_responses_alone()
    {
        // Regression: 200/204 responses without a body were rewritten into "Server error occurred".
        var (context, body) = await RunAsync(ctx => { ctx.Response.StatusCode = 204; return Task.CompletedTask; });

        Assert.Equal(204, context.Response.StatusCode);
        Assert.Equal("", body);
    }

    [Fact]
    public async Task Fills_empty_error_responses_with_problem_details()
    {
        var (context, body) = await RunAsync(ctx => { ctx.Response.StatusCode = 404; return Task.CompletedTask; });

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Contains("Resource not found", body);
    }

    [Fact]
    public async Task Keeps_error_responses_that_already_have_a_body_type()
    {
        var (context, body) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            return Task.CompletedTask;
        });

        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("", body);
    }

    [Fact]
    public async Task Converts_exceptions_to_500()
    {
        var (context, body) = await RunAsync(_ => throw new InvalidOperationException("boom"));

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains("Server error occurred", body);
        Assert.DoesNotContain("boom", body); // internal details are not leaked
    }

    [Fact]
    public async Task Converts_timeouts_to_408()
    {
        var (context, _) = await RunAsync(_ => throw new TimeoutException());

        Assert.Equal(408, context.Response.StatusCode);
    }
}
