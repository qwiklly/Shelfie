using System.Globalization;
using Shelfie.Client;
using Shelfie.Client.Storage;
using Shelfie.Shared;

namespace Shelfie.Client.Tests;

/// <summary>Everything here runs without any server: this is the offline mode of the app.</summary>
public sealed class LocalInventoryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"shelfie-test-{Guid.NewGuid():N}.db");
    private LocalDatabase _db = null!;
    private LocalInventory _inventory = null!;

    public ValueTask InitializeAsync()
    {
        _db = new LocalDatabase(_path);
        _inventory = new LocalInventory(_db);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private static ItemDto Milk(int quantity = 2, DateOnly? expires = null) => new()
    {
        CategoryId = BuiltInCategories.Products,
        Name = "Milk",
        Quantity = quantity,
        Location = "Холодильник",
        Barcode = "4600000000001",
        ExpirationDate = expires
    };

    private async Task<ItemDto> AddAsync(ItemDto item)
    {
        var result = await _inventory.SaveItemAsync(item);
        Assert.True(result.Success, result.ErrorText);
        return result.Item!;
    }

    [Fact]
    public async Task Fresh_device_has_built_in_categories()
    {
        var categories = await _inventory.GetCategoriesAsync();

        Assert.Equal([BuiltInCategories.Products, BuiltInCategories.Medications, BuiltInCategories.Books], categories.Select(c => c.Id));
    }

    [Fact]
    public async Task Items_are_saved_and_survive_reopening_the_database()
    {
        var milk = await AddAsync(Milk());
        await _db.DisposeAsync();

        _db = new LocalDatabase(_path);
        _inventory = new LocalInventory(_db);

        var reloaded = await _inventory.GetItemAsync(milk.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Milk", reloaded.Name);
        Assert.Equal("Холодильник", reloaded.Location);
    }

    [Fact]
    public async Task Every_change_is_pending_for_sync_and_written_to_history()
    {
        var milk = await AddAsync(Milk());
        milk.Quantity = 5;
        await _inventory.SaveItemAsync(milk);

        var history = await _inventory.GetHistoryAsync();
        Assert.Equal([ChangeTypes.Updated, ChangeTypes.Added], history.Select(h => h.ChangeType));
        Assert.Equal(3, history[0].QuantityChange);
        Assert.Equal(3, await _inventory.CountPendingChangesAsync()); // item + 2 history rows
    }

    [Fact]
    public async Task Validation_is_the_same_as_on_the_server()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        var noName = await _inventory.SaveItemAsync(new ItemDto { CategoryId = BuiltInCategories.Products, Name = "  " });
        var badYear = await _inventory.SaveItemAsync(new ItemDto { CategoryId = BuiltInCategories.Books, Name = "Dune", Fields = { ["year"] = "abc" } });
        var unknownCategory = await _inventory.SaveItemAsync(new ItemDto { CategoryId = Guid.NewGuid(), Name = "X" });

        Assert.False(noName.Success);
        Assert.False(badYear.Success);
        Assert.Contains("Год", badYear.ErrorText);
        Assert.False(unknownCategory.Success);
        Assert.Empty(await _inventory.GetItemsAsync());
    }

    [Fact]
    public async Task Minus_to_zero_adds_to_shopping_list_and_buying_restocks()
    {
        var milk = await AddAsync(Milk(quantity: 1));

        var after = await _inventory.AdjustQuantityAsync(milk.Id, -1);
        await _inventory.AdjustQuantityAsync(milk.Id, -1); // stays at 0, no duplicate entry

        Assert.Equal(0, after!.Quantity);
        var entry = Assert.Single(await _inventory.GetShoppingListAsync());
        Assert.Equal(milk.Id, entry.ItemId);

        await _inventory.SetShoppingCheckedAsync(entry.Id, true);
        Assert.Equal(1, await _inventory.CompleteCheckedAsync());

        Assert.Empty(await _inventory.GetShoppingListAsync());
        Assert.Equal(1, (await _inventory.GetItemAsync(milk.Id))!.Quantity);
        Assert.Equal(ChangeTypes.Restocked, (await _inventory.GetHistoryAsync()).First().ChangeType);
    }

    [Fact]
    public async Task Auto_shopping_can_be_disabled()
    {
        var milk = await AddAsync(Milk(quantity: 1));

        await _inventory.AdjustQuantityAsync(milk.Id, -1, addToShoppingWhenEmpty: false);

        Assert.Empty(await _inventory.GetShoppingListAsync());
    }

    [Fact]
    public async Task Manual_shopping_entries()
    {
        Assert.False((await _inventory.AddToShoppingListAsync(" ")).Success);
        await _inventory.AddToShoppingListAsync(" Bread ", 2);

        var entry = Assert.Single(await _inventory.GetShoppingListAsync());
        Assert.Equal("Bread", entry.Name);
        Assert.Equal(2, entry.Quantity);

        await _inventory.DeleteShoppingItemAsync(entry.Id);
        Assert.Empty(await _inventory.GetShoppingListAsync());
    }

    [Fact]
    public async Task Filters_search_location_and_expiry()
    {
        var today = _inventory.Today;
        await AddAsync(Milk(expires: today.AddDays(1)));
        var cheese = Milk(expires: today.AddDays(30));
        cheese.Name = "Cheese";
        cheese.Location = "Кладовая";
        await AddAsync(cheese);
        var old = Milk(expires: today.AddDays(-2));
        old.Name = "Old yogurt";
        await AddAsync(old);
        await AddAsync(new ItemDto { CategoryId = BuiltInCategories.Books, Name = "Dune", Fields = { ["author"] = "Frank Herbert" } });

        Assert.Equal("Old yogurt", (await _inventory.GetItemsAsync()).First().Name); // expired first
        Assert.Equal("Cheese", Assert.Single(await _inventory.GetItemsAsync(new ItemFilter(Location: "кладовая"))).Name);
        Assert.Equal("Milk", Assert.Single(await _inventory.GetItemsAsync(new ItemFilter(Status: ExpiryStatus.Soon))).Name);
        Assert.Equal("Old yogurt", Assert.Single(await _inventory.GetItemsAsync(new ItemFilter(Status: ExpiryStatus.Expired))).Name);
        Assert.Equal("Dune", Assert.Single(await _inventory.GetItemsAsync(new ItemFilter(Search: "herbert"))).Name);
        Assert.Equal(3, (await _inventory.GetItemsAsync(new ItemFilter(Search: "4600000000001"))).Count);
        Assert.Contains("Кладовая", await _inventory.GetLocationsAsync());
    }

    [Fact]
    public async Task Barcode_lookup_finds_known_items_offline()
    {
        await AddAsync(Milk());

        var found = await _inventory.FindByBarcodeAsync("4600000000001");

        Assert.Equal("Milk", found?.Name);
        Assert.Null(await _inventory.FindByBarcodeAsync("0000000000000"));
    }

    [Fact]
    public async Task Dashboard_counts_expiring_and_statistics()
    {
        var today = _inventory.Today;
        await AddAsync(Milk(expires: today.AddDays(1)));
        var old = Milk(quantity: 3, expires: today.AddDays(-1));
        old.Name = "Old";
        old = await AddAsync(old);
        var bread = Milk(quantity: 4);
        bread.Name = "Bread";
        bread = await AddAsync(bread);

        await _inventory.AdjustQuantityAsync(bread.Id, -1);           // consumed 1
        await _inventory.DeleteItemAsync(old.Id, ChangeTypes.Wasted); // wasted 3

        var stats = await _inventory.GetDashboardAsync(soonDays: 3);
        Assert.Equal(2, stats.TotalItems);
        Assert.Equal(1, stats.SoonCount);
        Assert.Equal(0, stats.ExpiredCount);
        Assert.Equal(1, stats.Consumed);
        Assert.Equal(3, stats.Wasted);
        Assert.Equal(0.25, stats.UsedShare);
        Assert.Equal(2, stats.ItemsPerCategory[BuiltInCategories.Products]);
    }

    [Fact]
    public async Task Custom_categories_with_typed_fields()
    {
        var wine = new CategoryDto
        {
            Name = " Вино ",
            Fields = [new FieldDefinition { Name = "Год", Type = FieldType.Number }, new FieldDefinition { Name = "Регион", Required = true }]
        };
        Assert.True((await _inventory.SaveCategoryAsync(wine)).Success);
        Assert.False((await _inventory.SaveCategoryAsync(new CategoryDto { Name = "вино" })).Success); // duplicate name

        var saved = (await _inventory.GetCategoriesAsync()).Single(c => c.Name == "Вино");
        Assert.All(saved.Fields, f => Assert.False(string.IsNullOrEmpty(f.Key)));
        Assert.Equal([1, 2], saved.Fields.Select(f => f.Order));

        var missingRequired = await _inventory.SaveItemAsync(new ItemDto { CategoryId = saved.Id, Name = "Merlot" });
        Assert.False(missingRequired.Success);
        var ok = await _inventory.SaveItemAsync(new ItemDto
        {
            CategoryId = saved.Id, Name = "Merlot",
            Fields = { [saved.Fields[0].Key] = "2015", [saved.Fields[1].Key] = "Бордо" }
        });
        Assert.True(ok.Success, ok.ErrorText);

        Assert.True((await _inventory.DeleteCategoryAsync(saved.Id)).Success);
        Assert.Empty(await _inventory.GetItemsAsync());
        Assert.False((await _inventory.DeleteCategoryAsync(BuiltInCategories.Products)).Success);
    }

    [Fact]
    public async Task Photo_path_is_local_only()
    {
        var milk = await AddAsync(Milk());
        var before = await _inventory.CountPendingChangesAsync();

        await _inventory.SetPhotoAsync(milk.Id, "/photos/milk.jpg");

        Assert.Equal("/photos/milk.jpg", await _inventory.GetPhotoPathAsync(milk.Id));
        Assert.Equal(before, await _inventory.CountPendingChangesAsync());
    }

    [Fact]
    public async Task Csv_export_works_offline()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        await AddAsync(Milk());

        var csv = await _inventory.ExportCsvAsync();

        Assert.Contains("Milk", csv);
        Assert.StartsWith("Категория;Название", csv);
    }

    [Fact]
    public async Task Changed_event_fires_on_writes()
    {
        int raised = 0;
        _inventory.Changed += (_, _) => raised++;

        await AddAsync(Milk());
        await _inventory.AddToShoppingListAsync("Bread");

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task Wipe_removes_everything()
    {
        await AddAsync(Milk());
        await _inventory.SetOwnerAsync("me@x.io");

        await _inventory.WipeAsync();

        Assert.Empty(await _inventory.GetItemsAsync());
        Assert.Null(await _inventory.GetOwnerAsync());
        Assert.Equal(0, await _inventory.CountPendingChangesAsync());
    }
}
