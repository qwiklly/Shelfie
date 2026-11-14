namespace Shelfie.Client;

public sealed record LogoutCheck(int PendingChanges, bool ServerReachable);

/// <summary>
/// Sign-in, guest mode and sign-out, keeping the local database consistent with the account:
/// guest data is uploaded to the first account that signs in; data of another account is wiped.
/// </summary>
public sealed class AccountManager
{
    private readonly ShelfieApi _api;
    private readonly LocalInventory _inventory;
    private readonly SyncService _sync;
    private readonly ITokenStore _tokens;

    public AccountManager(ShelfieApi api, LocalInventory inventory, SyncService sync, ITokenStore tokens)
    {
        _api = api;
        _inventory = inventory;
        _sync = sync;
        _tokens = tokens;
    }

    public async Task<string?> GetSignedInEmailAsync()
    {
        var token = await _api.GetValidTokenAsync();
        return token == null ? null : TokenInfo.Email(token);
    }

    public async Task<ApiResult<string>> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        email = email.Trim();
        var result = await _api.LoginAsync(email, password, ct);
        if (!result.Success)
            return result;

        var accountEmail = (TokenInfo.Email(result.Value!) ?? email).ToLowerInvariant();
        var owner = await _inventory.GetOwnerAsync();

        if (owner == null)
        {
            // Guest data (or a fresh install) becomes the account's data and is uploaded.
            await _inventory.MarkAllDirtyAsync();
            await _sync.ResetAsync();
        }
        else if (!string.Equals(owner, accountEmail, StringComparison.OrdinalIgnoreCase))
        {
            await _inventory.WipeAsync();
        }

        await _inventory.SetOwnerAsync(accountEmail);
        await _sync.SyncAsync(ct);
        return result;
    }

    public Task<ApiResult<Shelfie.Shared.ApiMessage>> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        _api.RegisterAsync(request, ct);

    /// <summary>Before signing out: how many local changes would be lost.</summary>
    public async Task<LogoutCheck> PrepareSignOutAsync(CancellationToken ct = default)
    {
        bool reachable = await _api.IsReachableAsync(ct);
        if (reachable)
            await _sync.SyncAsync(ct);
        return new LogoutCheck(await _inventory.CountPendingChangesAsync(), reachable);
    }

    /// <summary>Signs out and removes the account's data from this device.</summary>
    public async Task SignOutAsync()
    {
        _tokens.Clear();
        await _inventory.WipeAsync();
        await _sync.ResetAsync();
        await _sync.RefreshStatusAsync();
    }

    public async Task<ApiResult<Shelfie.Shared.ApiMessage>> DeleteAccountAsync(CancellationToken ct = default)
    {
        var email = await GetSignedInEmailAsync();
        if (email == null)
            return new ApiResult<Shelfie.Shared.ApiMessage>(false, null, "Войдите в аккаунт");

        var result = await _api.DeleteAccountAsync(email, ct);
        if (result.Success)
            await SignOutAsync();
        return result;
    }
}
