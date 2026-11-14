using SQLite;

namespace Shelfie.Client.Storage;

// Rows of the on-device SQLite database. Every synchronised row carries IsDirty = "changed locally, not yet
// accepted by the server". Dates are stored as ticks (UTC); DateOnly as day numbers.

[Table("Categories")]
internal sealed class CategoryRow
{
    [PrimaryKey] public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public string FieldsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("Items")]
internal sealed class ItemRow
{
    [PrimaryKey] public Guid Id { get; set; }
    [Indexed] public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Notes { get; set; }
    public int Quantity { get; set; }
    public double? Weight { get; set; }
    public string? WeightUnit { get; set; }
    public int? ExpirationDay { get; set; }
    public string? Location { get; set; }
    [Indexed] public string? Barcode { get; set; }
    public string FieldsJson { get; set; } = "{}";

    /// <summary>Photo file on this device. Not synchronised.</summary>
    public string? PhotoPath { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("Shopping")]
internal sealed class ShoppingRow
{
    [PrimaryKey] public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsChecked { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("History")]
internal sealed class HistoryRow
{
    [PrimaryKey] public Guid Id { get; set; }
    public Guid? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    [Indexed] public DateTime ChangeDate { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("Meta")]
internal sealed class MetaRow
{
    [PrimaryKey] public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

/// <summary>Opens the SQLite file and creates the tables.</summary>
public sealed class LocalDatabase : IAsyncDisposable
{
    internal SQLiteAsyncConnection Connection { get; }

    private readonly Lazy<Task> _init;

    public LocalDatabase(string path)
    {
        Connection = new SQLiteAsyncConnection(path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            storeDateTimeAsTicks: true);
        _init = new Lazy<Task>(CreateTablesAsync);
    }

    public Task EnsureCreatedAsync() => _init.Value;

    private async Task CreateTablesAsync()
    {
        await Connection.CreateTableAsync<CategoryRow>();
        await Connection.CreateTableAsync<ItemRow>();
        await Connection.CreateTableAsync<ShoppingRow>();
        await Connection.CreateTableAsync<HistoryRow>();
        await Connection.CreateTableAsync<MetaRow>();
    }

    internal async Task<string?> GetMetaAsync(string key) =>
        (await Connection.FindAsync<MetaRow>(key))?.Value;

    internal Task SetMetaAsync(string key, string? value) =>
        Connection.InsertOrReplaceAsync(new MetaRow { Key = key, Value = value });

    public async ValueTask DisposeAsync() => await Connection.CloseAsync();
}
