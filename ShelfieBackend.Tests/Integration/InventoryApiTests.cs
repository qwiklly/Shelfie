using System.Net;
using System.Net.Http.Json;
using Shelfie.Shared;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class InventoryApiTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private static ItemDto Milk(string name = "Milk", int quantity = 2, DateOnly? expires = null) => new()
    {
        CategoryId = BuiltInCategories.Products,
        Name = name,
        Brand = "Prostokvashino",
        Quantity = quantity,
        Weight = 1,
        WeightUnit = "л",
        Location = "Холодильник",
        Barcode = "4600000000001",
        ExpirationDate = expires ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10)
    };

    private static async Task<ItemDto> CreateAsync(HttpClient client, ItemDto item)
    {
        var response = await client.PostAsJsonAsync("api/items", item, Json, CT);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<ItemDto>();
    }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name, params FieldDefinition[] fields)
    {
        var response = await client.PostAsJsonAsync("api/categories", new CategoryDto { Name = name, Fields = fields.ToList() }, Json, CT);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<CategoryDto>();
    }

    // ---------------------------------------------------------------- categories

    [Fact]
    public async Task Everyone_has_the_three_built_in_categories()
    {
        var (client, _) = await factory.CreateUserAsync();

        var categories = await client.GetJsonAsync<List<CategoryDto>>("api/categories");

        Assert.Equal([BuiltInCategories.Products, BuiltInCategories.Medications, BuiltInCategories.Books], categories.Select(c => c.Id));
        var books = categories[2];
        Assert.Equal(CategoryKind.Books, books.Kind);
        Assert.Contains(books.Fields, f => f.Key == "year" && f.Type == FieldType.Number);
    }

    [Fact]
    public async Task Built_in_category_can_be_customised_but_not_deleted()
    {
        var (client, _) = await factory.CreateUserAsync();
        var products = (await client.GetJsonAsync<List<CategoryDto>>("api/categories")).First();
        products.Fields.Add(new FieldDefinition { Key = "shop", Name = "Магазин", Type = FieldType.Text, Order = 1 });

        var update = await client.PutAsJsonAsync($"api/categories/{products.Id}", products, Json, CT);
        var delete = await client.DeleteAsync($"api/categories/{products.Id}", CT);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        var reloaded = await client.GetJsonAsync<CategoryDto>($"api/categories/{products.Id}");
        Assert.Equal("Магазин", Assert.Single(reloaded.Fields).Name);
        Assert.Equal(CategoryKind.Products, reloaded.Kind);
    }

    [Fact]
    public async Task Category_validation()
    {
        var (client, _) = await factory.CreateUserAsync();
        await CreateCategoryAsync(client, "Wine");

        var duplicate = await client.PostAsJsonAsync("api/categories", new CategoryDto { Name = " wine " }, Json, CT);
        var empty = await client.PostAsJsonAsync("api/categories", new CategoryDto { Name = "  " }, Json, CT);
        var dupKeys = await client.PostAsJsonAsync("api/categories", new CategoryDto
        {
            Name = "Tools",
            Fields = [new() { Key = "a", Name = "A" }, new() { Key = "a", Name = "B" }]
        }, Json, CT);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, dupKeys.StatusCode);
    }

    [Fact]
    public async Task Renaming_and_reordering_fields_keeps_values()
    {
        // Regression of the old model: fields were matched by name, so a rename lost all values.
        var (client, _) = await factory.CreateUserAsync();
        var wine = await CreateCategoryAsync(client, "Wine",
            new FieldDefinition { Key = "region", Name = "Регион", Order = 1 },
            new FieldDefinition { Key = "year", Name = "Год", Type = FieldType.Number, Order = 2 });
        var merlot = await CreateAsync(client, new ItemDto { CategoryId = wine.Id, Name = "Merlot", Fields = { ["region"] = "Бордо", ["year"] = "2015" } });

        wine.Fields = [
            new FieldDefinition { Key = "year", Name = "Урожай", Type = FieldType.Number, Order = 1 },
            new FieldDefinition { Key = "region", Name = "Область", Order = 2 }];
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"api/categories/{wine.Id}", wine, Json, CT)).StatusCode);

        var reloaded = await client.GetJsonAsync<ItemDto>($"api/items/{merlot.Id}");
        Assert.Equal("Бордо", reloaded.Fields["region"]);
        Assert.Equal("2015", reloaded.Fields["year"]);
        var category = await client.GetJsonAsync<CategoryDto>($"api/categories/{wine.Id}");
        Assert.Equal(["Урожай", "Область"], category.Fields.Select(f => f.Name));
    }

    [Fact]
    public async Task Deleting_a_category_deletes_its_items()
    {
        var (client, _) = await factory.CreateUserAsync();
        var wine = await CreateCategoryAsync(client, "Wine");
        var merlot = await CreateAsync(client, new ItemDto { CategoryId = wine.Id, Name = "Merlot" });

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"api/categories/{wine.Id}", CT)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/items/{merlot.Id}", CT)).StatusCode);
        Assert.DoesNotContain(await client.GetJsonAsync<List<CategoryDto>>("api/categories"), c => c.Id == wine.Id);
    }

    // ---------------------------------------------------------------- items

    [Fact]
    public async Task Item_crud_by_id()
    {
        var (client, _) = await factory.CreateUserAsync();

        var created = await CreateAsync(client, Milk(" Milk "));
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Milk", created.Name);

        created.Quantity = 5;
        created.Name = "Milk 3.2%";
        var update = await client.PutAsJsonAsync($"api/items/{created.Id}", created, Json, CT);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var reloaded = await client.GetJsonAsync<ItemDto>($"api/items/{created.Id}");
        Assert.Equal("Milk 3.2%", reloaded.Name);
        Assert.Equal(5, reloaded.Quantity);
        Assert.Equal("Холодильник", reloaded.Location);

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"api/items/{created.Id}", CT)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/items/{created.Id}", CT)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_names_are_allowed_now_that_items_have_ids()
    {
        var (client, _) = await factory.CreateUserAsync();
        await CreateAsync(client, Milk());
        await CreateAsync(client, Milk());

        Assert.Equal(2, (await client.GetJsonAsync<List<ItemDto>>("api/items")).Count);
    }

    [Fact]
    public async Task Item_validation_uses_category_field_types()
    {
        var (client, _) = await factory.CreateUserAsync();
        var books = BuiltInCategories.Books;

        var badYear = await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = books, Name = "Dune", Fields = { ["year"] = "nineteen" } }, Json, CT);
        var negative = await client.PostAsJsonAsync("api/items", Milk(quantity: -1), Json, CT);
        var noName = await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = books, Name = " " }, Json, CT);
        var unknownCategory = await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = Guid.NewGuid(), Name = "X" }, Json, CT);
        var ok = await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = books, Name = "Dune", Fields = { ["year"] = "1965", ["unknown"] = "dropped" } }, Json, CT);

        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownCategory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var dune = await ok.ReadAsync<ItemDto>();
        Assert.False(dune.Fields.ContainsKey("unknown"));
    }

    [Fact]
    public async Task Required_fields_are_enforced()
    {
        var (client, _) = await factory.CreateUserAsync();
        var wine = await CreateCategoryAsync(client, "Wine", new FieldDefinition { Key = "region", Name = "Регион", Required = true });

        var missing = await client.PostAsJsonAsync("api/items", new ItemDto { CategoryId = wine.Id, Name = "Merlot" }, Json, CT);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Регион", (await missing.ReadAsync<ApiMessage>()).Message);
    }

    [Fact]
    public async Task Items_of_other_users_are_invisible_even_with_the_same_id()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        var aliceMilk = await CreateAsync(alice, Milk());

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"api/items/{aliceMilk.Id}", CT)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"api/items/{aliceMilk.Id}", CT)).StatusCode);

        // Bob may even use the same id: rows are keyed by (user, id).
        var bobCopy = Milk("Bob's milk");
        bobCopy.Id = aliceMilk.Id;
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync("api/items", bobCopy, Json, CT)).StatusCode);
        Assert.Equal("Milk", (await alice.GetJsonAsync<ItemDto>($"api/items/{aliceMilk.Id}")).Name);
    }

    [Fact]
    public async Task Filters_by_category_location_search_and_expiry()
    {
        var (client, _) = await factory.CreateUserAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateAsync(client, Milk("Milk", expires: today.AddDays(1)));
        var cheese = Milk("Cheese", expires: today.AddDays(30));
        cheese.Location = "Кладовая";
        await CreateAsync(client, cheese);
        await CreateAsync(client, new ItemDto { CategoryId = BuiltInCategories.Medications, Name = "Aspirin" });
        await CreateAsync(client, new ItemDto { CategoryId = BuiltInCategories.Books, Name = "Dune", Fields = { ["author"] = "Herbert" } });

        Assert.Equal(2, (await client.GetJsonAsync<List<ItemDto>>($"api/items?categoryId={BuiltInCategories.Products}")).Count);
        Assert.Equal("Cheese", Assert.Single(await client.GetJsonAsync<List<ItemDto>>("api/items?location=%D0%9A%D0%BB%D0%B0%D0%B4%D0%BE%D0%B2%D0%B0%D1%8F")).Name);
        Assert.Equal("Milk", Assert.Single(await client.GetJsonAsync<List<ItemDto>>("api/items?expiringWithinDays=3")).Name);
        Assert.Equal("Dune", Assert.Single(await client.GetJsonAsync<List<ItemDto>>("api/items?search=herb")).Name);
        Assert.Equal(2, (await client.GetJsonAsync<List<ItemDto>>("api/items?search=4600000000001")).Count);
    }

    [Fact]
    public async Task Adjust_quantity_to_zero_puts_item_on_shopping_list_once()
    {
        var (client, _) = await factory.CreateUserAsync();
        var milk = await CreateAsync(client, Milk(quantity: 1));

        var down = await client.PostAsJsonAsync($"api/items/{milk.Id}/adjust", new { delta = -1 }, Json, CT);
        await client.PostAsJsonAsync($"api/items/{milk.Id}/adjust", new { delta = -1 }, Json, CT); // already 0
        await client.PostAsJsonAsync($"api/items/{milk.Id}/adjust", new { delta = 3 }, Json, CT);
        await client.PostAsJsonAsync($"api/items/{milk.Id}/adjust", new { delta = -3 }, Json, CT);

        Assert.Equal(0, (await down.ReadAsync<ItemDto>()).Quantity);
        var entry = Assert.Single(await client.GetJsonAsync<List<ShoppingItemDto>>("api/shopping"));
        Assert.Equal(milk.Id, entry.ItemId);
        var history = await client.GetJsonAsync<List<HistoryEntryDto>>("api/history");
        Assert.Equal([ChangeTypes.Consumed, ChangeTypes.Restocked, ChangeTypes.Consumed, ChangeTypes.Added], history.Select(h => h.ChangeType));
    }

    [Theory]
    [InlineData(ChangeTypes.Consumed)]
    [InlineData(ChangeTypes.Wasted)]
    [InlineData(null)]
    public async Task Delete_reason_is_recorded_for_statistics(string? reason)
    {
        var (client, _) = await factory.CreateUserAsync();
        var milk = await CreateAsync(client, Milk());

        var url = reason == null ? $"api/items/{milk.Id}" : $"api/items/{milk.Id}?reason={reason}";
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(url, CT)).StatusCode);

        var last = (await client.GetJsonAsync<List<HistoryEntryDto>>("api/history")).First();
        Assert.Equal(reason ?? ChangeTypes.Removed, last.ChangeType);
        Assert.Equal(-2, last.QuantityChange);
        Assert.Equal("Milk", last.ItemName);
    }

    [Fact]
    public async Task Unknown_delete_reason_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        var milk = await CreateAsync(client, Milk());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"api/items/{milk.Id}?reason=Stolen", CT)).StatusCode);
    }

    [Fact]
    public async Task Csv_export_contains_items_and_extra_fields()
    {
        var (client, _) = await factory.CreateUserAsync();
        await CreateAsync(client, Milk("Milk; fresh"));
        await CreateAsync(client, new ItemDto { CategoryId = BuiltInCategories.Books, Name = "=cmd", Fields = { ["author"] = "Herbert" } });

        var response = await client.GetAsync("api/items/export", CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync(CT);
        Assert.Contains("\"Milk; fresh\"", csv);
        Assert.Contains("'=cmd", csv);        // formula injection neutralised
        Assert.Contains(Texts.Get("FieldAuthor"), csv); // header in the server's UI language
        Assert.Contains("Herbert", csv);
    }

    // ---------------------------------------------------------------- shopping & history

    [Fact]
    public async Task Shopping_list_crud()
    {
        var (client, _) = await factory.CreateUserAsync();

        var created = await (await client.PostAsJsonAsync("api/shopping", new ShoppingItemDto { Name = " Bread ", Quantity = 2 }, Json, CT)).ReadAsync<ShoppingItemDto>();
        created.IsChecked = true;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"api/shopping/{created.Id}", created, Json, CT)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("api/shopping", new ShoppingItemDto { Name = "" }, Json, CT)).StatusCode);

        var list = await client.GetJsonAsync<List<ShoppingItemDto>>("api/shopping");
        Assert.True(Assert.Single(list).IsChecked);
        Assert.Equal("Bread", list[0].Name);

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"api/shopping/{created.Id}", CT)).StatusCode);
        Assert.Empty(await client.GetJsonAsync<List<ShoppingItemDto>>("api/shopping"));
    }

    [Fact]
    public async Task Clearing_history_only_affects_the_current_user()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        await CreateAsync(alice, Milk());
        await CreateAsync(bob, Milk());

        Assert.Equal(HttpStatusCode.OK, (await alice.DeleteAsync("api/history", CT)).StatusCode);

        Assert.Empty(await alice.GetJsonAsync<List<HistoryEntryDto>>("api/history"));
        Assert.Single(await bob.GetJsonAsync<List<HistoryEntryDto>>("api/history"));
    }

    // ---------------------------------------------------------------- pipeline

    [Fact]
    public async Task Inventory_requires_authentication()
    {
        var anonymous = factory.CreateClient();
        foreach (var url in new[] { "api/items", "api/categories", "api/shopping", "api/history" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url, CT)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("api/sync", new SyncRequest(), Json, CT)).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        var response = await factory.CreateClient().GetAsync("health", CT);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_api_response_is_problem_json()
    {
        var response = await factory.CreateClient().GetAsync("api/items", CT);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.WwwAuthenticate.Count > 0);
    }

    [Fact]
    public async Task Unknown_route_is_404_problem_json()
    {
        var response = await factory.CreateClient().GetAsync("api/does-not-exist", CT);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
    public async Task Swagger_document_lists_the_new_endpoints()
    {
        var json = await factory.CreateClient().GetStringAsync("swagger/v1/swagger.json", CT);

        Assert.Contains("/api/items/{id}", json);
        Assert.Contains("/api/sync", json);
    }
}
