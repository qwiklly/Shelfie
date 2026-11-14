using System.Net;
using System.Net.Http.Json;
using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using ShelfieBackend.Tests.Infrastructure;
using static ShelfieBackend.Tests.Infrastructure.ApiClient;

namespace ShelfieBackend.Tests.Integration;

public class CustomCategoryTests(ShelfieApiFactory factory) : IClassFixture<ShelfieApiFactory>
{
    private static async Task<int> CreateCategoryAsync(HttpClient client, string name, params string[] fields)
    {
        var create = await client.PostAsJsonAsync("api/category/createCategory", new AddCategoryDTO { Name = name }, CT);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var category = (await client.GetJsonAsync<List<GetCategoriesDTO>>("api/category/getAllCategories"))
            .Single(c => c.Name == name.Trim());

        if (fields.Length > 0)
            Assert.Equal(HttpStatusCode.OK, (await SaveFieldsAsync(client, category.Id, fields)).StatusCode);

        return category.Id;
    }

    private static Task<HttpResponseMessage> SaveFieldsAsync(HttpClient client, int categoryId, params string[] fields) =>
        client.PostAsJsonAsync($"api/category/{categoryId}/addOrChangeFields", fields, CT);

    private static Task<List<CategoryFieldDTO>> GetFieldsAsync(HttpClient client, int categoryId) =>
        client.GetJsonAsync<List<CategoryFieldDTO>>($"api/category/{categoryId}/fields");

    private static Task<List<FieldValuesRequest>> GetRecordsAsync(HttpClient client, int categoryId) =>
        client.GetJsonAsync<List<FieldValuesRequest>>($"api/categoryValue/{categoryId}/values");

    private static Task<HttpResponseMessage> AddRecordAsync(HttpClient client, int categoryId, Dictionary<int, string> values, int recordId = 0) =>
        client.PostAsJsonAsync($"api/categoryValue/{categoryId}/save-values", new FieldValuesRequest { RecordId = recordId, FieldValues = values }, CT);

    // ---------- categories ----------

    [Fact]
    public async Task Category_name_must_be_non_empty_and_unique()
    {
        var (client, _) = await factory.CreateUserAsync();
        await CreateCategoryAsync(client, " Books ");

        var duplicate = await client.PostAsJsonAsync("api/category/createCategory", new AddCategoryDTO { Name = "Books" }, CT);
        var empty = await client.PostAsJsonAsync("api/category/createCategory", new AddCategoryDTO { Name = "   " }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Single(await client.GetJsonAsync<List<GetCategoriesDTO>>("api/category/getAllCategories"));
    }

    [Fact]
    public async Task Missing_category_returns_404_instead_of_500()
    {
        var (client, _) = await factory.CreateUserAsync();

        var response = await client.GetAsync("api/category/getOneCategory/999999", CT);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Field_endpoints_require_authentication()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/category/1/getCategoryFields", CT)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/categoryValue/1/values", CT)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("api/categoryValue/1/1", CT)).StatusCode);
    }

    [Fact]
    public async Task Other_users_cannot_touch_my_category()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(alice, "Wine", "Name");
        await AddRecordAsync(alice, categoryId, new() { [1] = "Merlot" });

        Assert.Empty(await GetFieldsAsync(bob, categoryId));
        Assert.Empty(await GetRecordsAsync(bob, categoryId));
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFieldsAsync(bob, categoryId, "Hacked")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AddRecordAsync(bob, categoryId, new() { [1] = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.DeleteAsync($"api/category/{categoryId}", CT)).StatusCode);

        Assert.Single(await GetRecordsAsync(alice, categoryId));
    }

    [Fact]
    public async Task Predefined_categories_cannot_be_deleted()
    {
        var (client, _) = await factory.CreateUserAsync();

        var response = await client.DeleteAsync("api/category/1", CT);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_category_removes_its_fields_and_values()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Tools", "Name", "Brand");
        await AddRecordAsync(client, categoryId, new() { [1] = "Hammer", [2] = "Bosch" });

        var response = await client.DeleteAsync($"api/category/{categoryId}", CT);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (fields, values) = await factory.WithDbAsync(db => Task.FromResult((
            db.CategoryFields.Count(f => f.CategoryId == categoryId),
            db.CategoryFieldValues.Count(v => v.CategoryId == categoryId))));
        Assert.Equal(0, fields);
        Assert.Equal(0, values);
    }

    // ---------- fields ----------

    [Fact]
    public async Task Fields_are_returned_in_order_with_their_keys()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", " Title ", "Author", "", "Author");

        var fields = await GetFieldsAsync(client, categoryId);

        Assert.Equal(["Title", "Author"], fields.Select(f => f.Name));
        Assert.Equal([1, 2], fields.Select(f => f.FieldOrder));
        Assert.Equal(["Title", "Author"], await client.GetJsonAsync<List<string>>($"api/category/{categoryId}/getCategoryFields"));
    }

    [Fact]
    public async Task Saving_the_same_fields_again_does_not_corrupt_records()
    {
        // Regression: every save used to append an extra empty value for the last field to every record,
        // which made GetFieldValues fail on duplicate keys and return an empty list.
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title", "Author");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune", [2] = "Herbert" });

        await SaveFieldsAsync(client, categoryId, "Title", "Author");
        await SaveFieldsAsync(client, categoryId, "Title", "Author");

        var record = Assert.Single(await GetRecordsAsync(client, categoryId));
        Assert.Equal("Dune", record.FieldValues[1]);
        Assert.Equal("Herbert", record.FieldValues[2]);
        var rows = await factory.WithDbAsync(db => Task.FromResult(db.CategoryFieldValues.Count(v => v.CategoryId == categoryId)));
        Assert.Equal(2, rows);
    }

    [Fact]
    public async Task Adding_fields_gives_every_record_an_empty_value_for_each_new_field()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune" });
        await AddRecordAsync(client, categoryId, new() { [1] = "Emma" });

        await SaveFieldsAsync(client, categoryId, "Title", "Author", "Year");

        var records = await GetRecordsAsync(client, categoryId);
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal([1, 2, 3], r.FieldValues.Keys.Order()));
        Assert.All(records, r => Assert.Equal("", r.FieldValues[3]));
    }

    [Fact]
    public async Task Removing_a_field_removes_its_values_and_keeps_the_others()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title", "Author");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune", [2] = "Herbert" });

        await SaveFieldsAsync(client, categoryId, "Author", "Genre");

        var fields = await GetFieldsAsync(client, categoryId);
        Assert.Equal([2, 3], fields.Select(f => f.FieldOrder));          // keys are not renumbered
        var record = Assert.Single(await GetRecordsAsync(client, categoryId));
        Assert.Equal("Herbert", record.FieldValues[2]);
        Assert.Equal("", record.FieldValues[3]);
        Assert.False(record.FieldValues.ContainsKey(1));
    }

    [Fact]
    public async Task Empty_field_list_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books");

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFieldsAsync(client, categoryId, " ", "")).StatusCode);
    }

    // ---------- records ----------

    [Fact]
    public async Task Server_assigns_record_ids_without_collisions_after_delete()
    {
        // Regression: the app used "records.Count + 1" as the new id, which collided after a delete.
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "A" });
        await AddRecordAsync(client, categoryId, new() { [1] = "B" });
        await client.DeleteAsync($"api/categoryValue/{categoryId}/1", CT);

        var add = await AddRecordAsync(client, categoryId, new() { [1] = "C" });

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var records = await GetRecordsAsync(client, categoryId);
        Assert.Equal([2, 3], records.Select(r => r.RecordId));
        Assert.Equal(["B", "C"], records.Select(r => r.FieldValues[1]));
    }

    [Fact]
    public async Task Explicit_duplicate_record_id_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "A" }, recordId: 7);

        var duplicate = await AddRecordAsync(client, categoryId, new() { [1] = "B" }, recordId: 7);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Record_with_only_empty_or_unknown_fields_is_rejected()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");

        Assert.Equal(HttpStatusCode.BadRequest, (await AddRecordAsync(client, categoryId, new() { [1] = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AddRecordAsync(client, categoryId, new() { [42] = "x" })).StatusCode);
        Assert.Empty(await GetRecordsAsync(client, categoryId));
    }

    [Fact]
    public async Task Record_values_are_trimmed_and_missing_fields_stored_empty()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title", "Author");

        await AddRecordAsync(client, categoryId, new() { [1] = "  Dune  " });

        var record = Assert.Single(await GetRecordsAsync(client, categoryId));
        Assert.Equal("Dune", record.FieldValues[1]);
        Assert.Equal("", record.FieldValues[2]);
    }

    [Fact]
    public async Task Update_changes_values_and_fills_fields_missing_in_the_record()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune" });
        // Simulate a record created by an old version that has no value for a later field.
        await SaveFieldsAsync(client, categoryId, "Title", "Author");
        await factory.WithDbAsync(async db =>
        {
            db.CategoryFieldValues.RemoveRange(db.CategoryFieldValues.Where(v => v.CategoryId == categoryId && v.FieldOrder == 2));
            return await db.SaveChangesAsync(CT);
        });

        var update = await client.PutAsJsonAsync($"api/categoryValue/{categoryId}/update-values",
            new FieldValuesRequest { RecordId = 1, FieldValues = new() { [1] = "Dune Messiah ", [2] = "Herbert" } }, CT);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var record = Assert.Single(await GetRecordsAsync(client, categoryId));
        Assert.Equal("Dune Messiah", record.FieldValues[1]);
        Assert.Equal("Herbert", record.FieldValues[2]);
    }

    [Fact]
    public async Task Update_of_missing_record_returns_400()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");

        var update = await client.PutAsJsonAsync($"api/categoryValue/{categoryId}/update-values",
            new FieldValuesRequest { RecordId = 5, FieldValues = new() { [1] = "x" } }, CT);

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Record_operations_are_written_to_history_with_a_readable_name()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune" });
        await client.DeleteAsync($"api/categoryValue/{categoryId}/1", CT);

        var history = await client.GetJsonAsync<List<HistoryRecordDTO>>("api/application/getAllHistory");

        Assert.Equal(["Removed", "Added"], history.Select(h => h.ChangeType));
        Assert.All(history, h => Assert.Equal("Books: Dune", h.ItemName));
    }

    [Fact]
    public async Task Tolerates_duplicate_rows_left_by_older_versions()
    {
        var (client, _) = await factory.CreateUserAsync();
        var categoryId = await CreateCategoryAsync(client, "Books", "Title");
        await AddRecordAsync(client, categoryId, new() { [1] = "Dune" });
        await factory.WithDbAsync(async db =>
        {
            db.CategoryFieldValues.Add(new CategoryFieldValue { CategoryId = categoryId, RecordId = 1, FieldOrder = 1, Value = "" });
            return await db.SaveChangesAsync(CT);
        });

        var records = await GetRecordsAsync(client, categoryId);

        Assert.Single(records);
    }
}
