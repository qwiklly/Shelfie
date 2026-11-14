using System.Net;
using Shelfie.Client;
using Shelfie.Shared;

namespace Shelfie.Client.Tests;

public class NotificationPlannerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0);
    private static readonly TimeOnly TenAm = new(10, 0);

    private static ItemDto Item(string name, int daysLeft, int quantity = 1) => new()
    {
        Name = name, Quantity = quantity, ExpirationDate = DateOnly.FromDateTime(Now).AddDays(daysLeft)
    };

    [Fact]
    public void One_digest_per_warning_day()
    {
        var plan = NotificationPlanner.Plan([Item("Milk", 5), Item("Cheese", 5), Item("Bread", 10)], Now, daysBefore: 2, TenAm, false);

        Assert.Equal(2, plan.Count);
        Assert.Equal(new DateTime(2026, 11, 17, 10, 0, 0), plan[0].NotifyAt);
        Assert.Contains("Milk", plan[0].Body);
        Assert.Contains("Cheese", plan[0].Body);
        Assert.Contains("2", plan[0].Title);
        Assert.Equal(new DateTime(2026, 11, 22, 10, 0, 0), plan[1].NotifyAt);
    }

    [Fact]
    public void Warning_day_in_the_past_is_shown_shortly_once_per_day()
    {
        var plan = NotificationPlanner.Plan([Item("Milk", 1)], Now, daysBefore: 3, TenAm, alreadyNotifiedToday: false);
        Assert.Equal(Now.AddMinutes(1), Assert.Single(plan).NotifyAt);

        Assert.Empty(NotificationPlanner.Plan([Item("Milk", 1)], Now, daysBefore: 3, TenAm, alreadyNotifiedToday: true));
    }

    [Fact]
    public void Expired_used_up_and_undated_items_are_ignored()
    {
        var plan = NotificationPlanner.Plan(
            [Item("Expired", -1), Item("Empty", 5, quantity: 0), new ItemDto { Name = "No date", Quantity = 1 }],
            Now, 2, TenAm, false);

        Assert.Empty(plan);
    }

    [Fact]
    public void Long_lists_are_shortened()
    {
        var items = Enumerable.Range(1, 5).Select(i => Item($"Item{i}", 7)).ToList();

        var body = Assert.Single(NotificationPlanner.Plan(items, Now, 2, TenAm, false)).Body;

        Assert.EndsWith("и ещё 2", body);
    }
}

public class ProductLookupTests
{
    [Theory]
    [InlineData("1 l", 1.0, "л")]
    [InlineData("500 g", 500.0, "г")]
    [InlineData("0,5 л", 0.5, "л")]
    [InlineData("1.5kg", 1.5, "кг")]
    [InlineData("6 x 330 ml", 330.0, "мл")]
    [InlineData("33 cl", 330.0, "мл")]
    public void Parses_quantity(string text, double weight, string unit)
    {
        var (w, u) = ProductLookup.ParseQuantity(text);
        Assert.Equal(weight, w!.Value, 3);
        Assert.Equal(unit, u);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("big pack")]
    public void Unknown_quantity_gives_nothing(string? text) =>
        Assert.Equal((null, null), ProductLookup.ParseQuantity(text));

    [Theory]
    [InlineData("4600000000001", true)]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    [InlineData("46000000000a1", false)]
    [InlineData(null, false)]
    public void Validates_barcodes(string? code, bool valid) =>
        Assert.Equal(valid, ProductLookup.IsValidBarcode(code));

    [Fact]
    public void Parses_open_food_facts_answer_preferring_russian_name()
    {
        var info = ProductLookup.Parse(new ProductLookup.OffResponse
        {
            Status = 1,
            Product = new() { ProductName = "Milk", ProductNameRu = "Молоко", Brands = "Простоквашино, Danone", Quantity = "930 ml" }
        });

        Assert.Equal(new ProductInfo("Молоко", "Простоквашино", 930, "мл", "openfoodfacts"), info);
    }

    [Fact]
    public void Unknown_product_gives_null()
    {
        Assert.Null(ProductLookup.Parse(new ProductLookup.OffResponse { Status = 0 }));
        Assert.Null(ProductLookup.Parse(new ProductLookup.OffResponse { Status = 1, Product = new() { ProductName = " " } }));
    }

    [Fact]
    public async Task Network_failure_is_not_an_error()
    {
        var lookup = new ProductLookup(new HttpClient(new FailingHandler()), new LocalInventory(new Storage.LocalDatabase(":memory:")));

        Assert.Null(await lookup.LookupAsync("4600000000001", TestContext.Current.CancellationToken));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}

public class ApiErrorTests
{
    private static Task<string> Read(HttpStatusCode status, string body) =>
        ShelfieApi.ReadErrorAsync(new HttpResponseMessage(status) { Content = new StringContent(body) }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Reads_flag_message() =>
        Assert.Equal("User already exists", await Read(HttpStatusCode.BadRequest, """{"flag":false,"message":"User already exists"}"""));

    [Fact]
    public async Task Reads_validation_errors() =>
        Assert.Equal("Пароль слабый\nEmail неверный",
            await Read(HttpStatusCode.BadRequest, """{"title":"x","errors":{"Password":["Пароль слабый"],"Email":["Email неверный"]}}"""));

    [Fact]
    public async Task Reads_problem_details() =>
        Assert.Equal("Resource not found", await Read(HttpStatusCode.NotFound, """{"title":"Not Found","detail":"Resource not found"}"""));

    [Fact]
    public async Task Falls_back_to_status_text() =>
        Assert.Equal("Ошибка сервера (502)", await Read(HttpStatusCode.BadGateway, "<html>bad gateway</html>"));

    [Fact]
    public async Task Server_that_never_answers_times_out_as_offline()
    {
        // Regression (seen on the emulator): a sync to a host that accepts but never answers hung in "Syncing…".
        var api = new ShelfieApi(new HttpClient(new HangingHandler()), () => "http://example.test", new InMemoryTokenStore())
        {
            RequestTimeout = TimeSpan.FromMilliseconds(200)
        };

        var started = DateTime.UtcNow;
        var result = await api.LoginAsync("a@b.c", "x", TestContext.Current.CancellationToken);

        Assert.True(result.IsOffline);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_offline()
    {
        var api = new ShelfieApi(new HttpClient(new HangingHandler()), () => "http://example.test", new InMemoryTokenStore());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.LoginAsync("a@b.c", "x", cts.Token));
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage();
        }
    }

    [Fact]
    public async Task Offline_server_is_reported_not_thrown()
    {
        var api = new ShelfieApi(new HttpClient(), () => "http://127.0.0.1:1", new InMemoryTokenStore());

        var result = await api.LoginAsync("a@b.c", "x", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.IsOffline);
        Assert.False(await api.IsReachableAsync(TestContext.Current.CancellationToken));
    }
}

public class SharedRulesTests
{
    [Theory]
    [InlineData(-1, ExpiryStatus.Expired)]
    [InlineData(0, ExpiryStatus.Soon)]
    [InlineData(3, ExpiryStatus.Soon)]
    [InlineData(4, ExpiryStatus.Fresh)]
    public void Expiry_status(int daysLeft, ExpiryStatus expected)
    {
        var today = new DateOnly(2026, 11, 14);
        Assert.Equal(expected, Expiry.GetStatus(today.AddDays(daysLeft), today, soonDays: 3));
    }

    [Fact]
    public void No_date_means_no_status() =>
        Assert.Equal(ExpiryStatus.None, Expiry.GetStatus(null, new DateOnly(2026, 1, 1)));

    [Theory]
    [InlineData(FieldType.Number, "12,5", true)]
    [InlineData(FieldType.Number, "abc", false)]
    [InlineData(FieldType.Date, "2026-11-14", true)]
    [InlineData(FieldType.Date, "14.11.2026", false)]
    [InlineData(FieldType.Boolean, "true", true)]
    [InlineData(FieldType.Boolean, "yes", false)]
    [InlineData(FieldType.Text, "anything", true)]
    public void Field_value_types(FieldType type, string value, bool valid) =>
        Assert.Equal(valid, FieldValues.IsValid(type, value));

    [Fact]
    public void Merge_keeps_built_ins_first_and_hides_deleted_custom_categories()
    {
        var overridden = new CategoryDto { Id = BuiltInCategories.Books, Name = "Библиотека", IsDeleted = true };
        var custom = new CategoryDto { Id = Guid.NewGuid(), Name = "Вино" };
        var deleted = new CategoryDto { Id = Guid.NewGuid(), Name = "Старое", IsDeleted = true };

        var merged = BuiltInCategories.Merge([custom, overridden, deleted]);

        Assert.Equal(["Продукты", "Медикаменты", "Библиотека", "Вино"], merged.Select(c => c.Name));
        Assert.False(merged[2].IsDeleted);
    }

    [Fact]
    public void Csv_escapes_separators_quotes_and_formulas()
    {
        Assert.Equal("\"a;b\"", CsvExporter.Escape("a;b"));
        Assert.Equal("\"say \"\"hi\"\"\"", CsvExporter.Escape("say \"hi\""));
        Assert.Equal("'=1+1", CsvExporter.Escape("=1+1"));
        Assert.Equal("plain", CsvExporter.Escape("plain"));
    }

    [Fact]
    public void Normalize_drops_unknown_fields_and_trims()
    {
        var category = new CategoryDto { Fields = [new FieldDefinition { Key = "a", Name = "A" }] };
        var item = new ItemDto { Name = " x ", Brand = " ", Fields = { ["a"] = " 1 ", ["b"] = "2" } };

        ItemRules.Normalize(item, category);

        Assert.Equal("x", item.Name);
        Assert.Null(item.Brand);
        Assert.Equal(new Dictionary<string, string> { ["a"] = "1" }, item.Fields);
    }

    [Fact]
    public void Category_with_too_many_fields_is_invalid()
    {
        var category = new CategoryDto
        {
            Name = "Big",
            Fields = Enumerable.Range(0, ItemRules.MaxFields + 1).Select(i => new FieldDefinition { Key = $"k{i}", Name = $"F{i}" }).ToList()
        };

        Assert.NotEmpty(ItemRules.ValidateCategory(category));
    }
}
