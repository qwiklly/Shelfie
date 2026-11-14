using System.Net.Http.Json;
using Shelfie.Client;
using Shelfie.Client.Storage;
using Shelfie.Shared;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

/// <summary>A phone: its own SQLite file, token store and sync engine, talking to the in-memory API.</summary>
internal sealed class Device : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"shelfie-device-{Guid.NewGuid():N}.db");
    private readonly LocalDatabase _db;

    public Device(ShelfieApiFactory factory, Func<bool>? online = null)
    {
        _db = new LocalDatabase(_path);
        Inventory = new LocalInventory(_db);
        Tokens = new InMemoryTokenStore();
        var http = factory.CreateDefaultClient(new OfflineSwitch(online ?? (() => true)));
        Api = new ShelfieApi(http, () => "http://localhost", Tokens);
        Sync = new SyncService(Inventory, Api);
        Account = new AccountManager(Api, Inventory, Sync, Tokens);
    }

    public LocalInventory Inventory { get; }
    public InMemoryTokenStore Tokens { get; }
    public ShelfieApi Api { get; }
    public SyncService Sync { get; }
    public AccountManager Account { get; }

    public async Task<ItemDto> AddAsync(string name, Guid? category = null)
    {
        var result = await Inventory.SaveItemAsync(new ItemDto { CategoryId = category ?? BuiltInCategories.Products, Name = name });
        Assert.True(result.Success, result.ErrorText);
        return result.Item!;
    }

    public async Task<List<string>> NamesAsync() => (await Inventory.GetItemsAsync()).Select(i => i.Name).Order().ToList();

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { File.Delete(_path); } catch (IOException) { }
    }

    /// <summary>Simulates losing the connection.</summary>
    private sealed class OfflineSwitch(Func<bool> online) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            online() ? base.SendAsync(request, cancellationToken) : throw new HttpRequestException("No route to host");
    }
}

public class OfflineSyncTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private async Task<string> RegisterAsync()
    {
        var registration = NewRegistration();
        Assert.True((await factory.CreateClient().RegisterAsync(registration)).IsSuccessStatusCode);
        return registration.Email;
    }

    [Fact]
    public async Task Guest_data_is_uploaded_when_the_user_signs_in()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);

        // Used without an account first.
        await phone.AddAsync("Milk");
        Assert.Equal(SyncState.NotSignedIn, (await phone.Sync.SyncAsync(CT)).State);

        var login = await phone.Account.SignInAsync(email, DefaultPassword, CT);

        Assert.True(login.Success, login.Error);
        Assert.Equal(SyncState.Synced, phone.Sync.Status.State);
        Assert.Equal(0, phone.Sync.Status.PendingChanges);
        var server = await factory.LoginAsAsync(email, DefaultPassword);
        Assert.Equal("Milk", Assert.Single(await server.GetJsonAsync<List<ItemDto>>("api/items")).Name);
    }

    [Fact]
    public async Task Changes_made_offline_are_synced_when_the_server_is_back()
    {
        var email = await RegisterAsync();
        bool online = true;
        await using var phone = new Device(factory, () => online);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);

        online = false;
        var milk = await phone.AddAsync("Milk");
        await phone.Inventory.AdjustQuantityAsync(milk.Id, -1); // → 0 → shopping list
        var offline = await phone.Sync.SyncAsync(CT);

        Assert.Equal(SyncState.Offline, offline.State);
        Assert.True(offline.PendingChanges > 0);
        Assert.Equal(["Milk"], await phone.NamesAsync()); // the app keeps working

        online = true;
        var back = await phone.Sync.SyncAsync(CT);

        Assert.Equal(SyncState.Synced, back.State);
        Assert.Equal(0, back.PendingChanges);
        var server = await factory.LoginAsAsync(email, DefaultPassword);
        Assert.Equal(0, Assert.Single(await server.GetJsonAsync<List<ItemDto>>("api/items")).Quantity);
        Assert.Single(await server.GetJsonAsync<List<ShoppingItemDto>>("api/shopping"));
        var history = await server.GetJsonAsync<List<HistoryEntryDto>>("api/history");
        Assert.Equal([ChangeTypes.Added, ChangeTypes.Consumed], history.Select(h => h.ChangeType).Order());
    }

    [Fact]
    public async Task Two_devices_converge_and_last_edit_wins()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);
        await using var tablet = new Device(factory);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        await tablet.Account.SignInAsync(email, DefaultPassword, CT);

        var milk = await phone.AddAsync("Milk");
        await phone.Sync.SyncAsync(CT);
        await tablet.Sync.SyncAsync(CT);
        Assert.Equal(["Milk"], await tablet.NamesAsync());

        // Both edit the same item without syncing; the tablet edits later.
        var onPhone = (await phone.Inventory.GetItemAsync(milk.Id))!;
        onPhone.Name = "Milk (phone)";
        await phone.Inventory.SaveItemAsync(onPhone);
        await Task.Delay(20, CT);
        var onTablet = (await tablet.Inventory.GetItemAsync(milk.Id))!;
        onTablet.Name = "Milk (tablet)";
        await tablet.Inventory.SaveItemAsync(onTablet);

        await tablet.Sync.SyncAsync(CT);
        await phone.Sync.SyncAsync(CT);   // phone's older edit loses
        await tablet.Sync.SyncAsync(CT);

        Assert.Equal(["Milk (tablet)"], await phone.NamesAsync());
        Assert.Equal(["Milk (tablet)"], await tablet.NamesAsync());
    }

    [Fact]
    public async Task Deletions_and_categories_propagate_between_devices()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);
        await using var tablet = new Device(factory);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        await tablet.Account.SignInAsync(email, DefaultPassword, CT);

        var wine = new CategoryDto { Name = "Вино", Fields = [new FieldDefinition { Name = "Год", Type = FieldType.Number }] };
        Assert.True((await phone.Inventory.SaveCategoryAsync(wine)).Success);
        var merlot = await phone.AddAsync("Merlot", wine.Id);
        var bread = await phone.AddAsync("Bread");
        await phone.Sync.SyncAsync(CT);
        await tablet.Sync.SyncAsync(CT);

        Assert.Contains(await tablet.Inventory.GetCategoriesAsync(), c => c.Name == "Вино" && c.Fields.Single().Type == FieldType.Number);
        Assert.Equal(["Bread", "Merlot"], await tablet.NamesAsync());

        await tablet.Inventory.DeleteItemAsync(bread.Id, ChangeTypes.Consumed);
        await tablet.Inventory.DeleteCategoryAsync(wine.Id);
        await tablet.Sync.SyncAsync(CT);
        await phone.Sync.SyncAsync(CT);

        Assert.Empty(await phone.NamesAsync());
        Assert.DoesNotContain(await phone.Inventory.GetCategoriesAsync(), c => c.Id == wine.Id);
        Assert.Null(await phone.Inventory.GetItemAsync(merlot.Id));
    }

    [Fact]
    public async Task Edit_made_during_a_sync_is_not_lost()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        var milk = await phone.AddAsync("Milk");
        await phone.Sync.SyncAsync(CT);

        // Edit, sync (uploads v2), edit again before the next sync: v3 must stay pending, not be overwritten.
        var item = (await phone.Inventory.GetItemAsync(milk.Id))!;
        item.Name = "v2";
        await phone.Inventory.SaveItemAsync(item);
        await phone.Sync.SyncAsync(CT);
        item.Name = "v3";
        await phone.Inventory.SaveItemAsync(item);

        Assert.True(await phone.Inventory.CountPendingChangesAsync() > 0);
        await phone.Sync.SyncAsync(CT);

        var server = await factory.LoginAsAsync(email, DefaultPassword);
        Assert.Equal("v3", (await server.GetJsonAsync<ItemDto>($"api/items/{milk.Id}")).Name);
    }

    [Fact]
    public async Task Signing_in_as_another_account_replaces_local_data()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        await using var phone = new Device(factory);
        await phone.Account.SignInAsync(alice, DefaultPassword, CT);
        await phone.AddAsync("Alice's milk");
        await phone.Sync.SyncAsync(CT);

        phone.Tokens.Clear(); // session expired, Bob signs in on the same phone
        await phone.Account.SignInAsync(bob, DefaultPassword, CT);

        Assert.Empty(await phone.NamesAsync());
        var bobServer = await factory.LoginAsAsync(bob, DefaultPassword);
        Assert.Empty(await bobServer.GetJsonAsync<List<ItemDto>>("api/items")); // Alice's data did not leak to Bob
    }

    [Fact]
    public async Task Sign_out_reports_pending_changes_and_wipes_the_device()
    {
        var email = await RegisterAsync();
        bool online = true;
        await using var phone = new Device(factory, () => online);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        online = false;
        await phone.AddAsync("Unsynced");

        var check = await phone.Account.PrepareSignOutAsync(CT);
        Assert.False(check.ServerReachable);
        Assert.True(check.PendingChanges > 0);

        await phone.Account.SignOutAsync();

        Assert.Empty(await phone.NamesAsync());
        Assert.Null(await phone.Account.GetSignedInEmailAsync());
    }

    [Fact]
    public async Task Server_side_changes_reach_the_device()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        var server = await factory.LoginAsAsync(email, DefaultPassword);

        await server.PostAsJsonAsync("api/items", new ItemDto { CategoryId = BuiltInCategories.Books, Name = "Dune", Fields = { ["year"] = "1965" } }, Json, CT);
        await phone.Sync.SyncAsync(CT);

        var dune = Assert.Single(await phone.Inventory.GetItemsAsync());
        Assert.Equal("1965", dune.Fields["year"]);
        Assert.Contains(await phone.Inventory.GetHistoryAsync(), h => h.ItemName == "Dune");
    }

    [Fact]
    public async Task Wrong_password_and_offline_login_are_reported()
    {
        var email = await RegisterAsync();
        bool online = true;
        await using var phone = new Device(factory, () => online);

        var wrong = await phone.Account.SignInAsync(email, "Wrong123", CT);
        online = false;
        var offline = await phone.Account.SignInAsync(email, DefaultPassword, CT);

        Assert.False(wrong.Success);
        Assert.False(wrong.IsOffline);
        Assert.False(string.IsNullOrEmpty(wrong.Error));
        Assert.True(offline.IsOffline);
    }

    [Fact]
    public async Task Account_deletion_from_the_app()
    {
        var email = await RegisterAsync();
        await using var phone = new Device(factory);
        await phone.Account.SignInAsync(email, DefaultPassword, CT);
        await phone.AddAsync("Milk");

        var result = await phone.Account.DeleteAccountAsync(CT);

        Assert.True(result.Success, result.Error);
        Assert.Empty(await phone.NamesAsync());
        Assert.False((await factory.CreateClient().LoginAsync(email, DefaultPassword)).IsSuccessStatusCode);
    }
}
