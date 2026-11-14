using System.Net;
using System.Net.Http.Json;
using Shelfie.Shared;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class SyncApiTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private static async Task<SyncResponse> SyncAsync(HttpClient client, SyncRequest request)
    {
        var response = await client.PostAsJsonAsync("api/sync", request, Json, CT);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SyncResponse>();
    }

    private static ItemDto OfflineItem(string name, DateTime updatedAt) => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = BuiltInCategories.Products,
        Name = name,
        Quantity = 1,
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt
    };

    [Fact]
    public async Task First_sync_uploads_offline_changes_and_returns_them_with_a_cursor()
    {
        var (client, _) = await factory.CreateUserAsync();
        var item = OfflineItem("Milk", DateTime.UtcNow.AddHours(-1));
        var history = new HistoryEntryDto { Id = Guid.NewGuid(), ItemId = item.Id, ItemName = "Milk", ChangeType = ChangeTypes.Added, ChangeDate = item.UpdatedAt, UpdatedAt = item.UpdatedAt };

        var result = await SyncAsync(client, new SyncRequest { Items = [item], History = [history] });

        Assert.Empty(result.Rejected);
        Assert.Contains(result.Items, i => i.Id == item.Id);
        Assert.Contains(result.History, h => h.Id == history.Id);
        Assert.True(result.Cursor > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Milk", (await client.GetJsonAsync<ItemDto>($"api/items/{item.Id}")).Name);
    }

    [Fact]
    public async Task Second_sync_returns_only_what_changed_since_the_cursor()
    {
        var (client, _) = await factory.CreateUserAsync();
        var first = await SyncAsync(client, new SyncRequest { Items = [OfflineItem("Old", DateTime.UtcNow)] });

        var created = await (await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = BuiltInCategories.Products, Name = "New" }, Json, CT)).ReadAsync<ItemDto>();
        var second = await SyncAsync(client, new SyncRequest { Since = first.Cursor });

        Assert.Equal(created.Id, Assert.Single(second.Items).Id);
        Assert.Contains(second.History, h => h.ItemId == created.Id); // history written by the REST call
    }

    [Fact]
    public async Task Last_write_wins()
    {
        var (client, _) = await factory.CreateUserAsync();
        var now = DateTime.UtcNow;
        var item = OfflineItem("v1", now.AddMinutes(-10));
        await SyncAsync(client, new SyncRequest { Items = [item] });

        // Newer edit from device A wins …
        var newer = OfflineItem("v2 (newer)", now.AddMinutes(-5));
        newer.Id = item.Id;
        await SyncAsync(client, new SyncRequest { Items = [newer] });

        // … an older edit from device B (made offline earlier) does not overwrite it.
        var older = OfflineItem("v0 (older)", now.AddMinutes(-8));
        older.Id = item.Id;
        var result = await SyncAsync(client, new SyncRequest { Items = [older] });

        Assert.Equal("v2 (newer)", (await client.GetJsonAsync<ItemDto>($"api/items/{item.Id}")).Name);
        Assert.Equal("v2 (newer)", result.Items.Single(i => i.Id == item.Id).Name); // the device gets the winner back
    }

    [Fact]
    public async Task Deletions_travel_as_tombstones()
    {
        var (client, _) = await factory.CreateUserAsync();
        var item = OfflineItem("Milk", DateTime.UtcNow.AddMinutes(-2));
        var first = await SyncAsync(client, new SyncRequest { Items = [item] });

        await client.DeleteAsync($"api/items/{item.Id}", CT);
        var second = await SyncAsync(client, new SyncRequest { Since = first.Cursor });

        Assert.True(second.Items.Single(i => i.Id == item.Id).IsDeleted);
    }

    [Fact]
    public async Task First_sync_skips_tombstones()
    {
        var (client, _) = await factory.CreateUserAsync();
        var item = OfflineItem("Milk", DateTime.UtcNow.AddMinutes(-2));
        await SyncAsync(client, new SyncRequest { Items = [item] });
        await client.DeleteAsync($"api/items/{item.Id}", CT);

        var freshDevice = await SyncAsync(client, new SyncRequest());

        Assert.DoesNotContain(freshDevice.Items, i => i.Id == item.Id);
    }

    [Fact]
    public async Task Invalid_entities_are_rejected_individually()
    {
        var (client, _) = await factory.CreateUserAsync();
        var good = OfflineItem("Good", DateTime.UtcNow);
        var noName = OfflineItem("", DateTime.UtcNow);
        var unknownCategory = OfflineItem("Lost", DateTime.UtcNow);
        unknownCategory.CategoryId = Guid.NewGuid();

        var result = await SyncAsync(client, new SyncRequest { Items = [good, noName, unknownCategory] });

        Assert.Equal(new[] { noName.Id, unknownCategory.Id }.Order(), result.Rejected.Select(r => r.Id).Order());
        Assert.All(result.Rejected, r => Assert.Equal("item", r.Entity));
        Assert.Contains(result.Items, i => i.Id == good.Id);
    }

    [Fact]
    public async Task Category_and_its_items_can_arrive_in_the_same_sync()
    {
        var (client, _) = await factory.CreateUserAsync();
        var now = DateTime.UtcNow;
        var wine = new CategoryDto
        {
            Id = Guid.NewGuid(), Name = "Wine", UpdatedAt = now,
            Fields = [new FieldDefinition { Key = "year", Name = "Год", Type = FieldType.Number }]
        };
        var merlot = OfflineItem("Merlot", now);
        merlot.CategoryId = wine.Id;
        merlot.Fields["year"] = "2015";

        var result = await SyncAsync(client, new SyncRequest { Categories = [wine], Items = [merlot] });

        Assert.Empty(result.Rejected);
        Assert.Contains(await client.GetJsonAsync<List<CategoryDto>>("api/categories"), c => c.Name == "Wine");
    }

    [Fact]
    public async Task Deleting_a_category_on_a_device_deletes_its_items_on_the_server()
    {
        var (client, _) = await factory.CreateUserAsync();
        var now = DateTime.UtcNow.AddMinutes(-1);
        var wine = new CategoryDto { Id = Guid.NewGuid(), Name = "Wine", UpdatedAt = now };
        var merlot = OfflineItem("Merlot", now);
        merlot.CategoryId = wine.Id;
        await SyncAsync(client, new SyncRequest { Categories = [wine], Items = [merlot] });

        wine.IsDeleted = true;
        wine.UpdatedAt = DateTime.UtcNow;
        await SyncAsync(client, new SyncRequest { Categories = [wine] });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/items/{merlot.Id}", CT)).StatusCode);
    }

    [Fact]
    public async Task Built_in_categories_cannot_be_deleted_through_sync()
    {
        var (client, _) = await factory.CreateUserAsync();
        var products = new CategoryDto { Id = BuiltInCategories.Products, Name = "Еда", IsDeleted = true, UpdatedAt = DateTime.UtcNow };

        await SyncAsync(client, new SyncRequest { Categories = [products] });

        var categories = await client.GetJsonAsync<List<CategoryDto>>("api/categories");
        Assert.Equal("Еда", categories.Single(c => c.Id == BuiltInCategories.Products).Name);
    }

    [Fact]
    public async Task Device_clock_in_the_future_is_clamped()
    {
        var (client, _) = await factory.CreateUserAsync();
        var item = OfflineItem("From the future", DateTime.UtcNow.AddYears(1));
        await SyncAsync(client, new SyncRequest { Items = [item] });

        // A normal edit made now must still be able to win.
        var edit = OfflineItem("Edited now", DateTime.UtcNow.AddSeconds(1));
        edit.Id = item.Id;
        await SyncAsync(client, new SyncRequest { Items = [edit] });

        Assert.Equal("Edited now", (await client.GetJsonAsync<ItemDto>($"api/items/{item.Id}")).Name);
    }

    [Fact]
    public async Task Sync_never_touches_data_of_other_users()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        var aliceItem = OfflineItem("Alice's", DateTime.UtcNow.AddMinutes(-1));
        await SyncAsync(alice, new SyncRequest { Items = [aliceItem] });

        var hijack = OfflineItem("Bob's", DateTime.UtcNow);
        hijack.Id = aliceItem.Id;
        hijack.IsDeleted = true;
        var bobResult = await SyncAsync(bob, new SyncRequest { Items = [hijack] });

        Assert.Equal("Alice's", (await alice.GetJsonAsync<ItemDto>($"api/items/{aliceItem.Id}")).Name);
        Assert.DoesNotContain(bobResult.Items, i => i.Name == "Alice's");
    }

    [Fact]
    public async Task Resending_the_same_changes_is_idempotent()
    {
        var (client, _) = await factory.CreateUserAsync();
        var item = OfflineItem("Milk", DateTime.UtcNow.AddMinutes(-1));
        var history = new HistoryEntryDto { Id = Guid.NewGuid(), ItemName = "Milk", ChangeType = ChangeTypes.Added, UpdatedAt = item.UpdatedAt };
        var request = new SyncRequest { Items = [item], History = [history] };

        await SyncAsync(client, request);
        await SyncAsync(client, request);

        Assert.Single(await client.GetJsonAsync<List<ItemDto>>("api/items"));
        Assert.Single(await client.GetJsonAsync<List<HistoryEntryDto>>("api/history"));
    }
}
