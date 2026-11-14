using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Shelfie.Shared;

namespace Shelfie.Client;

/// <summary>Where the JWT lives on the device (SecureStorage in the app, memory in tests).</summary>
public interface ITokenStore
{
    Task<string?> GetAsync();
    Task SetAsync(string token);
    void Clear();
}

public sealed class InMemoryTokenStore : ITokenStore
{
    private string? _token;
    public Task<string?> GetAsync() => Task.FromResult(_token);
    public Task SetAsync(string token) { _token = token; return Task.CompletedTask; }
    public void Clear() => _token = null;
}

public sealed record ApiResult<T>(bool Success, T? Value, string? Error, bool IsOffline = false, HttpStatusCode? Status = null)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
}

public sealed class UserProfile
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
}

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
}

/// <summary>
/// HTTP access to the Shelfie server. Network failures are reported as <see cref="ApiResult{T}.IsOffline"/>
/// instead of exceptions, and server error messages are passed through so the UI can show them.
/// </summary>
public sealed class ShelfieApi
{
    private readonly HttpClient _http;
    private readonly Func<string> _baseUrl;
    private readonly ITokenStore _tokens;

    public ShelfieApi(HttpClient http, Func<string> baseUrl, ITokenStore tokens)
    {
        _http = http;
        _baseUrl = baseUrl;
        _tokens = tokens;
    }

    /// <summary>Stored token if it has not expired; an expired one is removed.</summary>
    public async Task<string?> GetValidTokenAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrEmpty(token))
            return null;
        if (TokenInfo.IsExpired(token))
        {
            _tokens.Clear();
            return null;
        }
        return token;
    }

    private Uri Url(string path)
    {
        var root = _baseUrl().TrimEnd('/');
        if (!root.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            root += "/api";
        return new Uri(root + "/" + path.TrimStart('/'));
    }

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            var root = _baseUrl().TrimEnd('/');
            if (root.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                root = root[..^4];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await _http.GetAsync(root + "/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<ApiResult<string>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await SendAsync<LoginResponse>(HttpMethod.Post, "application/login", new { email, password }, auth: false, ct);
        if (!result.Success)
            return new ApiResult<string>(false, null, result.Error, result.IsOffline, result.Status);
        if (result.Value is not { Flag: true } login || string.IsNullOrEmpty(login.JwtToken))
            return new ApiResult<string>(false, null, result.Value?.Message ?? Texts.Get("WrongCredentials"));

        await _tokens.SetAsync(login.JwtToken);
        return ApiResult<string>.Ok(login.JwtToken);
    }

    public Task<ApiResult<ApiMessage>> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        SendAsync<ApiMessage>(HttpMethod.Post, "application/register", request, auth: false, ct);

    public Task<ApiResult<UserProfile>> GetProfileAsync(string email, CancellationToken ct = default) =>
        SendAsync<UserProfile>(HttpMethod.Get, $"application/getUser?email={Uri.EscapeDataString(email)}", null, auth: true, ct);

    public Task<ApiResult<ApiMessage>> UpdateProfileAsync(string email, string? name, string? phone, string? newPassword, CancellationToken ct = default) =>
        SendAsync<ApiMessage>(HttpMethod.Put, $"application/updateUser/{Uri.EscapeDataString(email)}",
            new { name, phone, password = string.IsNullOrEmpty(newPassword) ? null : newPassword }, auth: true, ct);

    public Task<ApiResult<ApiMessage>> DeleteAccountAsync(string email, CancellationToken ct = default) =>
        SendAsync<ApiMessage>(HttpMethod.Delete, $"application/deleteUser?email={Uri.EscapeDataString(email)}", null, auth: true, ct);

    public Task<ApiResult<SyncResponse>> SyncAsync(SyncRequest request, CancellationToken ct = default) =>
        SendAsync<SyncResponse>(HttpMethod.Post, "sync", request, auth: true, ct);

    /// <summary>
    /// Upper bound for one request. A server that accepts the connection but never answers (sleeping laptop,
    /// captive Wi-Fi, emulator NAT) must not leave the app "syncing" forever.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, bool auth, CancellationToken callerCt)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(callerCt);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            var ct = timeout.Token;
            using var request = new HttpRequestMessage(method, Url(path));
            if (body != null)
                request.Content = JsonContent.Create(body, options: ShelfieJson.Options);
            if (auth)
            {
                var token = await GetValidTokenAsync();
                if (token == null)
                    return new ApiResult<T>(false, default, Texts.Get("SignInRequired"), Status: HttpStatusCode.Unauthorized);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(ShelfieJson.Options, ct);
                return value == null
                    ? new ApiResult<T>(false, default, Texts.Get("EmptyResponse"), Status: response.StatusCode)
                    : ApiResult<T>.Ok(value);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && auth)
                _tokens.Clear(); // token rejected (e.g. expired or the key changed)

            var error = await ReadErrorAsync(response, ct);
            // A login failure still carries a LoginResponse body with the reason.
            return new ApiResult<T>(false, default, error, Status: response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or UriFormatException)
        {
            // Cancelled by the caller → propagate; cancelled by our timeout → the server is unreachable.
            if (callerCt.IsCancellationRequested)
                throw;
            return new ApiResult<T>(false, default, Texts.Get("ServerUnavailable"), IsOffline: true);
        }
    }

    /// <summary>Extracts a human readable message from {flag,message}, ProblemDetails or validation errors.</summary>
    internal static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()))
                        .Where(m => !string.IsNullOrEmpty(m));
                    var joined = string.Join("\n", messages);
                    if (joined.Length > 0)
                        return joined;
                }
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString()!;
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Texts.Get("Unauthorized"),
            HttpStatusCode.Forbidden => Texts.Get("Forbidden"),
            HttpStatusCode.NotFound => Texts.Get("NotFound"),
            _ => Texts.Format("ServerError", (int)response.StatusCode)
        };
    }

    private sealed class LoginResponse
    {
        public bool Flag { get; set; }
        public string Message { get; set; } = string.Empty;
        public string JwtToken { get; set; } = string.Empty;
    }
}

public static class TokenInfo
{
    public static bool IsExpired(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo <= DateTime.UtcNow;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    public static string? Email(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .FirstOrDefault(c => c.Type is "email" or ClaimTypes.Email)?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
