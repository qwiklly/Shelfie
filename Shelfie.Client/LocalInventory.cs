using Shelfie.Client.Storage;
using Shelfie.Shared;

namespace Shelfie.Client;

public sealed record ItemFilter(Guid? CategoryId = null, string? Search = null, string? Location = null, ExpiryStatus? Status = null);

public sealed record SaveResult(bool Success, IReadOnlyList<string> Errors, ItemDto? Item = null)
{
    public static SaveResult Ok(ItemDto? item = null) => new(true, [], item);
    public static SaveResult Fail(params string[] errors) => new(false, errors);
    public string ErrorText => string.Join("\n", Errors);
}

public sealed class DashboardStats
{
    public Dictionary<Guid, int> ItemsPerCategory { get; init; } = new();
    public int TotalItems { get; init; }
    public int ExpiredCount { get; init; }
    public int SoonCount { get; init; }
    public List<ItemDto> Expiring { get; init; } = new();
    public int Consumed { get; init; }
    public int Wasted { get; init; }
    public int Added { get; init; }
    public int ShoppingCount { get; init; }

    /// <summary>Share of used-up items among those that left the inventory (0..1), or null when nothing left yet.</summary>
    public double? UsedShare => Consumed + Wasted == 0 ? null : (double)Consumed / (Consumed + Wasted);
}

/// <summary>
/// The inventory on this device. Everything the app shows comes from here, so it works without a server.
/// Every change is marked dirty and written to the history; <see cref="SyncService"/> uploads it later.
/// </summary>
public sealed class LocalInventory
{
    private const string OwnerKey = "owner";

    private readonly LocalDatabase _db;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalInventory(LocalDatabase db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Raised after every local change (and after a sync brought something new).</summary>
    public event EventHandler? Changed;

    internal LocalDatabase Database => _db;
    internal SemaphoreSlim Lock => _lock;
    private DateTime Now => _time.GetUtcNow().UtcDateTime;
    public DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    internal void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, bool notify = true)
    {
        await _db.EnsureCreatedAsync();
        await _lock.WaitAsync();
        T result;
        try
        {
            result = await action();
        }
        finally
        {
            _lock.Release();
        }

        if (notify)
            RaiseChanged();
        return result;
    }

    private async Task<List<T>> AllAsync<T>() where T : new()
    {
        await _db.EnsureCreatedAsync();
        return await _db.Connection.Table<T>().ToListAsync();
    }

    // ------------------------------------------------------------------ categories

    public async Task<List<CategoryDto>> GetCategoriesAsync() =>
        BuiltInCategories.Merge((await AllAsync<CategoryRow>()).Select(r => r.ToDto()));

    public async Task<CategoryDto?> GetCategoryAsync(Guid id) =>
        (await GetCategoriesAsync()).FirstOrDefault(c => c.Id == id);

    public Task<SaveResult> SaveCategoryAsync(CategoryDto category) => WriteAsync(async () =>
    {
        if (category.Id == Guid.Empty)
            category.Id = Guid.NewGuid();
        category.Name = category.Name?.Trim() ?? string.Empty;
        category.Kind = BuiltInCategories.KindOf(category.Id);
        category.IsDeleted = false;
        for (int i = 0; i < category.Fields.Count; i++)
        {
            var field = category.Fields[i];
            field.Name = field.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(field.Key))
                field.Key = NewFieldKey();
            field.Order = i + 1;
        }

        var errors = ItemRules.ValidateCategory(category);
        var existing = await GetCategoriesAsync();
        if (existing.Any(c => c.Id != category.Id && string.Equals(c.Name, category.Name, StringComparison.CurrentCultureIgnoreCase)))
            errors.Add(Texts.Get("CategoryExists"));
        if (errors.Count > 0)
            return new SaveResult(false, errors);

        category.UpdatedAt = Now;
        var row = await _db.Connection.FindAsync<CategoryRow>(category.Id) ?? new CategoryRow();
        row.Apply(category);
        row.IsDirty = true;
        await _db.Connection.InsertOrReplaceAsync(row);
        return SaveResult.Ok();
    });

    internal static string NewFieldKey() => "f" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Deletes a custom category and all of its items.</summary>
    public Task<SaveResult> DeleteCategoryAsync(Guid id) => WriteAsync(async () =>
    {
        if (BuiltInCategories.IsBuiltIn(id))
            return SaveResult.Fail(Texts.Get("BuiltInCannotDelete"));

        var row = await _db.Connection.FindAsync<CategoryRow>(id);
        if (row == null || row.IsDeleted)
            return SaveResult.Fail(Texts.Get("CategoryNotFound"));

        var now = Now;
        row.IsDeleted = true;
        row.UpdatedAt = now;
        row.IsDirty = true;
        await _db.Connection.UpdateAsync(row);

        foreach (var item in (await AllAsync<ItemRow>()).Where(i => i.CategoryId == id && !i.IsDeleted))
        {
            item.IsDeleted = true;
            item.UpdatedAt = now;
            item.IsDirty = true;
            await _db.Connection.UpdateAsync(item);
        }
        return SaveResult.Ok();
    });

    // ------------------------------------------------------------------ items

    public async Task<List<ItemDto>> GetItemsAsync(ItemFilter? filter = null, int soonDays = Expiry.DefaultSoonDays)
    {
        filter ??= new ItemFilter();
        var today = Today;
        IEnumerable<ItemDto> items = (await AllAsync<ItemRow>()).Where(r => !r.IsDeleted).Select(r => r.ToDto());

        if (filter.CategoryId != null)
            items = items.Where(i => i.CategoryId == filter.CategoryId);
        if (!string.IsNullOrWhiteSpace(filter.Location))
            items = items.Where(i => string.Equals(i.Location, filter.Location, StringComparison.CurrentCultureIgnoreCase));
        if (filter.Status != null)
            items = items.Where(i => Expiry.GetStatus(i.ExpirationDate, today, soonDays) == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            items = items.Where(i =>
                i.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (i.Brand?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
                || (i.Notes?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
                || i.Barcode == term
                || i.Fields.Values.Any(v => v.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        // Items running out first; then by name.
        return items.OrderBy(i => i.ExpirationDate ?? DateOnly.MaxValue).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<ItemDto?> GetItemAsync(Guid id)
    {
        await _db.EnsureCreatedAsync();
        var row = await _db.Connection.FindAsync<ItemRow>(id);
        return row == null || row.IsDeleted ? null : row.ToDto();
    }

    public async Task<string?> GetPhotoPathAsync(Guid id)
    {
        await _db.EnsureCreatedAsync();
        return (await _db.Connection.FindAsync<ItemRow>(id))?.PhotoPath;
    }

    /// <summary>Most recently changed item with this barcode — lets the scanner work offline for known products.</summary>
    public async Task<ItemDto?> FindByBarcodeAsync(string barcode) =>
        (await AllAsync<ItemRow>())
            .Where(r => r.Barcode == barcode && !r.IsDeleted)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => r.ToDto())
            .FirstOrDefault();

    /// <summary>Suggestions plus every location already used.</summary>
    public async Task<List<string>> GetLocationsAsync()
    {
        var used = (await AllAsync<ItemRow>())
            .Where(r => !r.IsDeleted && !string.IsNullOrWhiteSpace(r.Location))
            .Select(r => r.Location!);
        return StorageLocations.Suggestions.Concat(used).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Creates (Id empty or unknown) or updates an item.</summary>
    public Task<SaveResult> SaveItemAsync(ItemDto item) => WriteAsync(async () =>
    {
        var category = await GetCategoryAsync(item.CategoryId);
        if (category == null)
            return SaveResult.Fail(Texts.Get("CategoryNotFound"));

        ItemRules.Normalize(item, category);
        item.IsDeleted = false;
        var errors = ItemRules.Validate(item, category);
        if (errors.Count > 0)
            return new SaveResult(false, errors);

        var now = Now;
        var row = item.Id == Guid.Empty ? null : await _db.Connection.FindAsync<ItemRow>(item.Id);
        bool isNew = row == null || row.IsDeleted;
        int oldQuantity = isNew ? 0 : row!.Quantity;

        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();
        item.CreatedAt = isNew ? now : row!.CreatedAt;
        item.UpdatedAt = now;

        row ??= new ItemRow();
        row.Apply(item);
        row.IsDirty = true;
        await _db.Connection.InsertOrReplaceAsync(row);

        await AddHistoryAsync(row, isNew ? ChangeTypes.Added : ChangeTypes.Updated, row.Quantity - oldQuantity);
        return SaveResult.Ok(row.ToDto());
    });

    /// <summary>
    /// Quick −/+. When the quantity reaches zero the item is put on the shopping list
    /// (if <paramref name="addToShoppingWhenEmpty"/>).
    /// </summary>
    public Task<ItemDto?> AdjustQuantityAsync(Guid id, int delta, bool addToShoppingWhenEmpty = true) => WriteAsync(async () =>
    {
        var row = await _db.Connection.FindAsync<ItemRow>(id);
        if (row == null || row.IsDeleted)
            return null;

        int newQuantity = Math.Max(0, row.Quantity + delta);
        int change = newQuantity - row.Quantity;
        if (change == 0)
            return row.ToDto();

        row.Quantity = newQuantity;
        row.UpdatedAt = Now;
        row.IsDirty = true;
        await _db.Connection.UpdateAsync(row);
        await AddHistoryAsync(row, change < 0 ? ChangeTypes.Consumed : ChangeTypes.Restocked, change);

        if (newQuantity == 0 && addToShoppingWhenEmpty)
            await EnsureOnShoppingListAsync(row);

        return row.ToDto();
    });

    /// <summary>Deletes an item. Reason: Removed, Consumed (used up) or Wasted (thrown away) — for statistics.</summary>
    public Task<bool> DeleteItemAsync(Guid id, string reason = ChangeTypes.Removed) => WriteAsync(async () =>
    {
        var row = await _db.Connection.FindAsync<ItemRow>(id);
        if (row == null || row.IsDeleted)
            return false;

        row.IsDeleted = true;
        row.UpdatedAt = Now;
        row.IsDirty = true;
        await _db.Connection.UpdateAsync(row);
        await AddHistoryAsync(row, reason, -row.Quantity);
        return true;
    });

    public Task SetPhotoAsync(Guid id, string? path) => WriteAsync(async () =>
    {
        var row = await _db.Connection.FindAsync<ItemRow>(id);
        if (row != null)
        {
            row.PhotoPath = path;
            await _db.Connection.UpdateAsync(row); // local only: not dirty
        }
        return true;
    });

    private async Task AddHistoryAsync(ItemRow item, string changeType, int quantityChange)
    {
        var now = Now;
        await _db.Connection.InsertAsync(new HistoryRow
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            ItemName = item.Name,
            CategoryId = item.CategoryId,
            ChangeType = changeType,
            QuantityChange = quantityChange,
            ChangeDate = now,
            UpdatedAt = now,
            IsDirty = true
        });
    }

    // ------------------------------------------------------------------ shopping list

    public async Task<List<ShoppingItemDto>> GetShoppingListAsync() =>
        (await AllAsync<ShoppingRow>())
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.IsChecked).ThenBy(r => r.CreatedAt)
            .Select(r => r.ToDto())
            .ToList();

    public Task<SaveResult> AddToShoppingListAsync(string name, int quantity = 1, Guid? itemId = null, Guid? categoryId = null) => WriteAsync(async () =>
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return SaveResult.Fail(Texts.Get("NameRequired"));

        if (itemId != null && (await AllAsync<ShoppingRow>()).Any(r => r.ItemId == itemId && !r.IsDeleted && !r.IsChecked))
            return SaveResult.Ok(); // already listed

        var now = Now;
        await _db.Connection.InsertAsync(new ShoppingRow
        {
            Id = Guid.NewGuid(),
            Name = name,
            Quantity = Math.Max(1, quantity),
            ItemId = itemId,
            CategoryId = categoryId,
            CreatedAt = now,
            UpdatedAt = now,
            IsDirty = true
        });
        return SaveResult.Ok();
    });

    private async Task EnsureOnShoppingListAsync(ItemRow item)
    {
        if ((await AllAsync<ShoppingRow>()).Any(r => r.ItemId == item.Id && !r.IsDeleted && !r.IsChecked))
            return;

        var now = Now;
        await _db.Connection.InsertAsync(new ShoppingRow
        {
            Id = Guid.NewGuid(), Name = item.Name, Quantity = 1, ItemId = item.Id, CategoryId = item.CategoryId,
            CreatedAt = now, UpdatedAt = now, IsDirty = true
        });
    }

    public Task SetShoppingCheckedAsync(Guid id, bool isChecked) => UpdateShoppingAsync(id, r => r.IsChecked = isChecked);

    public Task DeleteShoppingItemAsync(Guid id) => UpdateShoppingAsync(id, r => r.IsDeleted = true);

    private Task UpdateShoppingAsync(Guid id, Action<ShoppingRow> change) => WriteAsync(async () =>
    {
        var row = await _db.Connection.FindAsync<ShoppingRow>(id);
        if (row == null || row.IsDeleted)
            return false;

        change(row);
        row.UpdatedAt = Now;
        row.IsDirty = true;
        await _db.Connection.UpdateAsync(row);
        return true;
    });

    /// <summary>
    /// "Bought": checked entries are removed; when an entry belongs to an inventory item, its quantity is
    /// increased by the bought amount.
    /// </summary>
    public Task<int> CompleteCheckedAsync() => WriteAsync(async () =>
    {
        var now = Now;
        int count = 0;
        foreach (var entry in (await AllAsync<ShoppingRow>()).Where(r => r.IsChecked && !r.IsDeleted))
        {
            entry.IsDeleted = true;
            entry.UpdatedAt = now;
            entry.IsDirty = true;
            await _db.Connection.UpdateAsync(entry);
            count++;

            if (entry.ItemId is Guid itemId)
            {
                var item = await _db.Connection.FindAsync<ItemRow>(itemId);
                if (item != null && !item.IsDeleted)
                {
                    item.Quantity += entry.Quantity;
                    item.UpdatedAt = now;
                    item.IsDirty = true;
                    await _db.Connection.UpdateAsync(item);
                    await AddHistoryAsync(item, ChangeTypes.Restocked, entry.Quantity);
                }
            }
        }
        return count;
    });

    // ------------------------------------------------------------------ history & statistics

    public async Task<List<HistoryEntryDto>> GetHistoryAsync(int take = 200) =>
        (await AllAsync<HistoryRow>())
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.ChangeDate)
            .Take(take)
            .Select(r => r.ToDto())
            .ToList();

    public Task ClearHistoryAsync() => WriteAsync(async () =>
    {
        var now = Now;
        foreach (var row in (await AllAsync<HistoryRow>()).Where(r => !r.IsDeleted))
        {
            row.IsDeleted = true;
            row.UpdatedAt = now;
            row.IsDirty = true;
            await _db.Connection.UpdateAsync(row);
        }
        return true;
    });

    public async Task<DashboardStats> GetDashboardAsync(int soonDays = Expiry.DefaultSoonDays, int statsDays = 30)
    {
        var today = Today;
        var items = await GetItemsAsync(soonDays: soonDays);
        var since = Now.AddDays(-statsDays);
        var history = (await AllAsync<HistoryRow>()).Where(h => !h.IsDeleted && h.ChangeDate >= since).ToList();
        var expiring = items
            .Where(i => Expiry.GetStatus(i.ExpirationDate, today, soonDays) is ExpiryStatus.Soon or ExpiryStatus.Expired)
            .OrderBy(i => i.ExpirationDate)
            .ToList();

        return new DashboardStats
        {
            ItemsPerCategory = items.GroupBy(i => i.CategoryId).ToDictionary(g => g.Key, g => g.Count()),
            TotalItems = items.Count,
            ExpiredCount = expiring.Count(i => i.ExpirationDate < today),
            SoonCount = expiring.Count(i => i.ExpirationDate >= today),
            Expiring = expiring,
            // "Consumed" also covers −1 taps; "Wasted" only explicit throw-aways.
            Consumed = history.Where(h => h.ChangeType == ChangeTypes.Consumed).Sum(h => Math.Abs(h.QuantityChange)),
            Wasted = history.Where(h => h.ChangeType == ChangeTypes.Wasted).Sum(h => Math.Abs(h.QuantityChange)),
            Added = history.Count(h => h.ChangeType == ChangeTypes.Added),
            ShoppingCount = (await GetShoppingListAsync()).Count(s => !s.IsChecked)
        };
    }

    public async Task<string> ExportCsvAsync() => CsvExporter.Export(await GetItemsAsync(), await GetCategoriesAsync());

    // ------------------------------------------------------------------ account / sync bookkeeping

    /// <summary>E-mail of the account the local data belongs to; null for guest data.</summary>
    public async Task<string?> GetOwnerAsync()
    {
        await _db.EnsureCreatedAsync();
        return await _db.GetMetaAsync(OwnerKey);
    }

    public async Task SetOwnerAsync(string? email)
    {
        await _db.EnsureCreatedAsync();
        await _db.SetMetaAsync(OwnerKey, email);
    }

    public async Task<int> CountPendingChangesAsync()
    {
        await _db.EnsureCreatedAsync();
        var c = _db.Connection;
        return await c.Table<CategoryRow>().CountAsync(r => r.IsDirty)
             + await c.Table<ItemRow>().CountAsync(r => r.IsDirty)
             + await c.Table<ShoppingRow>().CountAsync(r => r.IsDirty)
             + await c.Table<HistoryRow>().CountAsync(r => r.IsDirty);
    }

    /// <summary>Removes all local data (used when another account signs in, on logout and account deletion).</summary>
    public Task WipeAsync() => WriteAsync(async () =>
    {
        var c = _db.Connection;
        await c.DeleteAllAsync<CategoryRow>();
        await c.DeleteAllAsync<ItemRow>();
        await c.DeleteAllAsync<ShoppingRow>();
        await c.DeleteAllAsync<HistoryRow>();
        await c.DeleteAllAsync<MetaRow>();
        return true;
    });

    /// <summary>Marks everything as changed so that the next sync uploads it (guest data → new account).</summary>
    internal Task MarkAllDirtyAsync() => WriteAsync(async () =>
    {
        var c = _db.Connection;
        await c.ExecuteAsync("UPDATE Categories SET IsDirty = 1");
        await c.ExecuteAsync("UPDATE Items SET IsDirty = 1");
        await c.ExecuteAsync("UPDATE Shopping SET IsDirty = 1");
        await c.ExecuteAsync("UPDATE History SET IsDirty = 1");
        return true;
    }, notify: false);
}
