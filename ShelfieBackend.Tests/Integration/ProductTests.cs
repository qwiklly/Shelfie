using System.Net;
using System.Net.Http.Json;
using ShelfieBackend.DTOs;
using ShelfieBackend.States;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class ProductTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private static AddProductDTO Milk(string name = "Milk") => new()
    {
        Name = name,
        Creator = "Prostokvashino",
        Description = "2.5%",
        ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
        Quantity = 2,
        Weight = 1,
        WeightUnit = "л"
    };

    [Fact]
    public async Task Products_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("api/application/getAll", CT);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Added_product_is_listed_with_all_fields()
    {
        var (client, _) = await factory.CreateUserAsync();

        var add = await client.PostAsJsonAsync("api/application/add", Milk(), CT);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var product = Assert.Single(await client.GetJsonAsync<List<GetProductDTO>>("api/application/getAll"));
        Assert.True(product.Id > 0);
        Assert.Equal("Milk", product.Name);
        Assert.Equal("2.5%", product.Description);
        Assert.Equal(Constants.ProductsCategoryId, product.CategoryId);
        Assert.Equal("л", product.WeightUnit);
    }

    [Fact]
    public async Task Product_name_is_trimmed_and_must_be_unique_per_user()
    {
        var (client, _) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/application/add", Milk("  Bread "), CT);

        var duplicate = await client.PostAsJsonAsync("api/application/add", Milk("Bread"), CT);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var products = await client.GetJsonAsync<List<GetProductDTO>>("api/application/getAll");
        Assert.Equal("Bread", Assert.Single(products).Name);
    }

    [Fact]
    public async Task Products_of_other_users_are_invisible()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        await alice.PostAsJsonAsync("api/application/add", Milk("Secret cheese"), CT);

        Assert.Empty(await bob.GetJsonAsync<List<GetProductDTO>>("api/application/getAll"));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("api/application/get/Secret%20cheese", CT)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.DeleteAsync("api/application/delete/Secret%20cheese", CT)).StatusCode);
        Assert.Single(await alice.GetJsonAsync<List<GetProductDTO>>("api/application/getAll"));
    }

    [Fact]
    public async Task Product_cannot_be_attached_to_another_users_category()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        await alice.PostAsJsonAsync("api/category/createCategory", new AddCategoryDTO { Name = "Private" }, CT);
        var aliceCategory = (await alice.GetJsonAsync<List<GetCategoriesDTO>>("api/category/getAllCategories")).Single();

        var model = Milk();
        model.CategoryId = aliceCategory.Id;
        await bob.PostAsJsonAsync("api/application/add", model, CT);

        var product = Assert.Single(await bob.GetJsonAsync<List<GetProductDTO>>("api/application/getAll"));
        Assert.Equal(Constants.ProductsCategoryId, product.CategoryId);
    }

    [Fact]
    public async Task Partial_update_keeps_unspecified_values_and_records_quantity_change()
    {
        var (client, _) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/application/add", Milk(), CT);

        var update = await client.PutAsJsonAsync("api/application/update/Milk", new UpdateProductDTO { Quantity = 5 }, CT);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var product = await client.GetJsonAsync<GetProductDTO>("api/application/get/Milk");
        Assert.Equal(5, product.Quantity);
        Assert.Equal("л", product.WeightUnit);     // used to be wiped to ""
        Assert.Equal(1, product.Weight);
        Assert.Equal("Prostokvashino", product.Creator);

        var history = await client.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory");
        var updated = history.First();
        Assert.Equal(Constants.ChangeUpdated, updated.ChangeType);
        Assert.Equal(3, updated.QuantityChange);
    }

    [Fact]
    public async Task Rename_to_an_existing_name_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/application/add", Milk("A"), CT);
        await client.PostAsJsonAsync("api/application/add", Milk("B"), CT);

        var response = await client.PutAsJsonAsync("api/application/update/A", new UpdateProductDTO { Name = "B" }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_of_missing_product_returns_400()
    {
        var (client, _) = await factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync("api/application/update/Nope", new UpdateProductDTO { Quantity = 1 }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_writes_history_with_product_name_not_creator()
    {
        var (client, _) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/application/add", Milk(), CT);

        var delete = await client.DeleteAsync("api/application/delete/Milk", CT);

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Empty(await client.GetJsonAsync<List<GetProductDTO>>("api/application/getAll"));
        var removed = (await client.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory")).First();
        Assert.Equal(Constants.ChangeRemoved, removed.ChangeType);
        Assert.Equal("Milk", removed.ItemName);
        Assert.Equal(-2, removed.QuantityChange);
    }

    [Fact]
    public async Task Expired_products_contain_only_past_dates()
    {
        var (client, _) = await factory.CreateUserAsync();
        var fresh = Milk("Fresh");
        var old = Milk("Old");
        old.ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var noDate = Milk("NoDate");
        noDate.ExpirationDate = null;
        foreach (var p in new[] { fresh, old, noDate })
            await client.PostAsJsonAsync("api/application/add", p, CT);

        var expired = await client.GetJsonAsync<List<GetProductDTO>>("api/application/getExpiredProducts");

        Assert.Equal("Old", Assert.Single(expired).Name);
        Assert.Equal("л", expired[0].WeightUnit);
    }

    [Fact]
    public async Task Negative_quantity_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        var model = Milk();
        model.Quantity = -1;

        var response = await client.PostAsJsonAsync("api/application/add", model, CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Medicament_goes_to_medication_category_and_history_type()
    {
        var (client, _) = await factory.CreateUserAsync();

        var add = await client.PostAsJsonAsync("api/medication/add", Milk("Aspirin"), CT);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        await client.DeleteAsync("api/medication/delete/Aspirin", CT);

        var history = await client.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory");
        Assert.All(history, h => Assert.Equal(Constants.ItemTypeMedicament, h.ItemType));
        Assert.All(history, h => Assert.Equal("Aspirin", h.ItemName));

        await client.PostAsJsonAsync("api/medication/add", Milk("Ibuprofen"), CT);
        var med = Assert.Single(await client.GetJsonAsync<List<GetProductDTO>>("api/medication/getAll"));
        Assert.Equal(Constants.MedicationsCategoryId, med.CategoryId);
    }

    [Fact]
    public async Task Medicament_update_and_expired_list_work()
    {
        var (client, _) = await factory.CreateUserAsync();
        await client.PostAsJsonAsync("api/medication/add", Milk("Aspirin"), CT);

        var update = await client.PutAsJsonAsync("api/medication/update/Aspirin", new UpdateProductDTO
        {
            Name = "Aspirin C",
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3)
        }, CT);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var expired = await client.GetJsonAsync<List<GetProductDTO>>("api/medication/getExpiredMedicaments");
        Assert.Equal("Aspirin C", Assert.Single(expired).Name);
    }
}
