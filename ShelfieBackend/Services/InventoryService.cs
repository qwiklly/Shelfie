using Microsoft.EntityFrameworkCore;
using Shelfie.Shared;
using ShelfieBackend.Data;
using ShelfieBackend.Models;

namespace ShelfieBackend.Services
{
    public sealed record ItemQuery(Guid? CategoryId = null, string? Search = null, string? Location = null, int? ExpiringWithinDays = null);

    /// <summary>Result of a command: either a value or a validation/lookup error.</summary>
    public sealed record ServiceResult<T>(T? Value, string? Error, bool NotFound = false)
    {
        public bool Ok => Error == null;
        public static ServiceResult<T> Success(T value) => new(value, null);
        public static ServiceResult<T> Fail(string error) => new(default, error);
        public static ServiceResult<T> Missing(string error) => new(default, error, NotFound: true);
    }

    /// <summary>
    /// Inventory of one user: categories, items, shopping list and history.
    /// REST endpoints and the sync endpoint share this logic.
    /// </summary>
    public class InventoryService(ApplicationDbContext db, TimeProvider time)
    {
        private DateTime Now => time.GetUtcNow().UtcDateTime;

        // ------------------------------------------------------------------ categories

        public async Task<List<CategoryDto>> GetCategoriesAsync(int userId, CancellationToken ct)
        {
            var stored = await db.ItemCategories.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(ct);
            return BuiltInCategories.Merge(stored.Select(ToDto));
        }

        public async Task<CategoryDto?> GetCategoryAsync(int userId, Guid id, CancellationToken ct) =>
            (await GetCategoriesAsync(userId, ct)).FirstOrDefault(c => c.Id == id);

        public async Task<ServiceResult<CategoryDto>> SaveCategoryAsync(int userId, CategoryDto dto, CancellationToken ct)
        {
            if (dto.Id == Guid.Empty)
                dto.Id = Guid.NewGuid();
            dto.Name = dto.Name?.Trim() ?? string.Empty;
            dto.Kind = BuiltInCategories.KindOf(dto.Id);
            dto.IsDeleted = false;
            dto.UpdatedAt = Now;

            var errors = ItemRules.ValidateCategory(dto);
            if (errors.Count > 0)
                return ServiceResult<CategoryDto>.Fail(string.Join("; ", errors));

            bool duplicate = (await GetCategoriesAsync(userId, ct))
                .Any(c => c.Id != dto.Id && string.Equals(c.Name, dto.Name, StringComparison.CurrentCultureIgnoreCase));
            if (duplicate)
                return ServiceResult<CategoryDto>.Fail("Категория с таким названием уже существует");

            var entity = await db.ItemCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == dto.Id, ct);
            if (entity == null)
            {
                entity = new ItemCategory { UserId = userId, Id = dto.Id };
                db.ItemCategories.Add(entity);
            }
            Apply(dto, entity);
            entity.ServerModifiedAt = Now;

            await db.SaveChangesAsync(ct);
            return ServiceResult<CategoryDto>.Success(ToDto(entity));
        }

        public async Task<ServiceResult<bool>> DeleteCategoryAsync(int userId, Guid id, CancellationToken ct)
        {
            if (BuiltInCategories.IsBuiltIn(id))
                return ServiceResult<bool>.Fail("Встроенную категорию нельзя удалить");

            var entity = await db.ItemCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id && !c.IsDeleted, ct);
            if (entity == null)
                return ServiceResult<bool>.Missing("Категория не найдена");

            entity.IsDeleted = true;
            entity.UpdatedAt = Now;
            entity.ServerModifiedAt = Now;
            await DeleteItemsOfCategoryAsync(userId, id, ct);

            await db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Success(true);
        }

        private async Task DeleteItemsOfCategoryAsync(int userId, Guid categoryId, CancellationToken ct)
        {
            var items = await db.Items.Where(i => i.UserId == userId && i.CategoryId == categoryId && !i.IsDeleted).ToListAsync(ct);
            foreach (var item in items)
            {
                item.IsDeleted = true;
                item.UpdatedAt = Now;
                item.ServerModifiedAt = Now;
            }
        }

        // ------------------------------------------------------------------ items

        public async Task<List<ItemDto>> GetItemsAsync(int userId, ItemQuery query, CancellationToken ct)
        {
            var items = db.Items.AsNoTracking().Where(i => i.UserId == userId && !i.IsDeleted);

            if (query.CategoryId != null)
                items = items.Where(i => i.CategoryId == query.CategoryId);
            if (!string.IsNullOrWhiteSpace(query.Location))
                items = items.Where(i => i.Location == query.Location);
            if (query.ExpiringWithinDays != null)
            {
                var limit = DateOnly.FromDateTime(Now).AddDays(query.ExpiringWithinDays.Value);
                items = items.Where(i => i.ExpirationDate != null && i.ExpirationDate <= limit);
            }

            var list = (await items.ToListAsync(ct)).Select(ToDto);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                list = list.Where(i => Matches(i, term));
            }

            return list.OrderBy(i => i.ExpirationDate ?? DateOnly.MaxValue).ThenBy(i => i.Name).ToList();
        }

        private static bool Matches(ItemDto item, string term) =>
            item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || (item.Brand?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || item.Barcode == term
            || item.Fields.Values.Any(v => v.Contains(term, StringComparison.CurrentCultureIgnoreCase));

        public async Task<ItemDto?> GetItemAsync(int userId, Guid id, CancellationToken ct)
        {
            var entity = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.UserId == userId && i.Id == id && !i.IsDeleted, ct);
            return entity == null ? null : ToDto(entity);
        }

        public async Task<ServiceResult<ItemDto>> CreateItemAsync(int userId, ItemDto dto, CancellationToken ct)
        {
            if (dto.Id == Guid.Empty)
                dto.Id = Guid.NewGuid();
            else if (await db.Items.AnyAsync(i => i.UserId == userId && i.Id == dto.Id, ct))
                return ServiceResult<ItemDto>.Fail("Вещь с таким id уже существует");

            dto.CreatedAt = Now;
            return await SaveItemAsync(userId, dto, isNew: true, ct);
        }

        public async Task<ServiceResult<ItemDto>> UpdateItemAsync(int userId, Guid id, ItemDto dto, CancellationToken ct)
        {
            var existing = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.UserId == userId && i.Id == id && !i.IsDeleted, ct);
            if (existing == null)
                return ServiceResult<ItemDto>.Missing("Вещь не найдена");

            dto.Id = id;
            dto.CreatedAt = existing.CreatedAt;
            return await SaveItemAsync(userId, dto, isNew: false, ct);
        }

        private async Task<ServiceResult<ItemDto>> SaveItemAsync(int userId, ItemDto dto, bool isNew, CancellationToken ct)
        {
            var category = await GetCategoryAsync(userId, dto.CategoryId, ct);
            if (category == null)
                return ServiceResult<ItemDto>.Fail("Категория не найдена");

            ItemRules.Normalize(dto, category);
            dto.IsDeleted = false;
            dto.UpdatedAt = Now;

            var errors = ItemRules.Validate(dto, category);
            if (errors.Count > 0)
                return ServiceResult<ItemDto>.Fail(string.Join("; ", errors));

            var entity = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.Id == dto.Id, ct);
            int oldQuantity = entity?.Quantity ?? 0;
            if (entity == null)
            {
                entity = new Item { UserId = userId, Id = dto.Id };
                db.Items.Add(entity);
            }
            Apply(dto, entity);
            entity.ServerModifiedAt = Now;

            AddHistory(userId, entity, isNew ? ChangeTypes.Added : ChangeTypes.Updated, entity.Quantity - oldQuantity);
            await db.SaveChangesAsync(ct);
            return ServiceResult<ItemDto>.Success(ToDto(entity));
        }

        /// <summary>Removes an item. <paramref name="reason"/>: Removed, Consumed or Wasted (for statistics).</summary>
        public async Task<ServiceResult<bool>> DeleteItemAsync(int userId, Guid id, string? reason, CancellationToken ct)
        {
            reason ??= ChangeTypes.Removed;
            if (reason is not (ChangeTypes.Removed or ChangeTypes.Consumed or ChangeTypes.Wasted))
                return ServiceResult<bool>.Fail("reason должен быть Removed, Consumed или Wasted");

            var entity = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.Id == id && !i.IsDeleted, ct);
            if (entity == null)
                return ServiceResult<bool>.Missing("Вещь не найдена");

            entity.IsDeleted = true;
            entity.UpdatedAt = Now;
            entity.ServerModifiedAt = Now;
            AddHistory(userId, entity, reason, -entity.Quantity);

            await db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Success(true);
        }

        /// <summary>Quick −/+ of the quantity. Reaching zero puts the item on the shopping list.</summary>
        public async Task<ServiceResult<ItemDto>> AdjustQuantityAsync(int userId, Guid id, int delta, CancellationToken ct)
        {
            var entity = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.Id == id && !i.IsDeleted, ct);
            if (entity == null)
                return ServiceResult<ItemDto>.Missing("Вещь не найдена");

            int newQuantity = Math.Max(0, entity.Quantity + delta);
            int change = newQuantity - entity.Quantity;
            if (change == 0)
                return ServiceResult<ItemDto>.Success(ToDto(entity));

            entity.Quantity = newQuantity;
            entity.UpdatedAt = Now;
            entity.ServerModifiedAt = Now;
            AddHistory(userId, entity, change < 0 ? ChangeTypes.Consumed : ChangeTypes.Restocked, change);

            if (newQuantity == 0)
                await EnsureOnShoppingListAsync(userId, entity, ct);

            await db.SaveChangesAsync(ct);
            return ServiceResult<ItemDto>.Success(ToDto(entity));
        }

        private async Task EnsureOnShoppingListAsync(int userId, Item item, CancellationToken ct)
        {
            bool listed = await db.ShoppingItems.AnyAsync(s => s.UserId == userId && s.ItemId == item.Id && !s.IsDeleted && !s.IsChecked, ct);
            if (listed)
                return;

            db.ShoppingItems.Add(new ShoppingItem
            {
                UserId = userId,
                Id = Guid.NewGuid(),
                Name = item.Name,
                Quantity = 1,
                ItemId = item.Id,
                CategoryId = item.CategoryId,
                CreatedAt = Now,
                UpdatedAt = Now,
                ServerModifiedAt = Now
            });
        }

        private void AddHistory(int userId, Item item, string changeType, int quantityChange) =>
            db.HistoryEntries.Add(new HistoryEntry
            {
                UserId = userId,
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                ItemName = item.Name,
                CategoryId = item.CategoryId,
                ChangeType = changeType,
                QuantityChange = quantityChange,
                ChangeDate = Now,
                UpdatedAt = Now,
                ServerModifiedAt = Now
            });

        public async Task<string> ExportCsvAsync(int userId, CancellationToken ct) =>
            CsvExporter.Export(await GetItemsAsync(userId, new ItemQuery(), ct), await GetCategoriesAsync(userId, ct));

        // ------------------------------------------------------------------ shopping list

        public async Task<List<ShoppingItemDto>> GetShoppingListAsync(int userId, CancellationToken ct) =>
            (await db.ShoppingItems.AsNoTracking()
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .ToListAsync(ct))
            .OrderBy(s => s.IsChecked).ThenBy(s => s.CreatedAt)
            .Select(ToDto).ToList();

        public async Task<ServiceResult<ShoppingItemDto>> SaveShoppingItemAsync(int userId, Guid? id, ShoppingItemDto dto, CancellationToken ct)
        {
            dto.Name = dto.Name?.Trim() ?? string.Empty;
            if (dto.Name.Length == 0)
                return ServiceResult<ShoppingItemDto>.Fail("Название не может быть пустым");
            if (dto.Quantity < 1)
                return ServiceResult<ShoppingItemDto>.Fail("Количество должно быть больше нуля");

            ShoppingItem? entity;
            if (id == null)
            {
                entity = new ShoppingItem { UserId = userId, Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id, CreatedAt = Now };
                if (await db.ShoppingItems.AnyAsync(s => s.UserId == userId && s.Id == entity.Id, ct))
                    return ServiceResult<ShoppingItemDto>.Fail("Запись с таким id уже существует");
                db.ShoppingItems.Add(entity);
            }
            else
            {
                entity = await db.ShoppingItems.FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id && !s.IsDeleted, ct);
                if (entity == null)
                    return ServiceResult<ShoppingItemDto>.Missing("Запись не найдена");
            }

            entity.Name = dto.Name;
            entity.Quantity = dto.Quantity;
            entity.IsChecked = dto.IsChecked;
            entity.ItemId = dto.ItemId;
            entity.CategoryId = dto.CategoryId;
            entity.UpdatedAt = Now;
            entity.ServerModifiedAt = Now;

            await db.SaveChangesAsync(ct);
            return ServiceResult<ShoppingItemDto>.Success(ToDto(entity));
        }

        public async Task<ServiceResult<bool>> DeleteShoppingItemAsync(int userId, Guid id, CancellationToken ct)
        {
            var entity = await db.ShoppingItems.FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id && !s.IsDeleted, ct);
            if (entity == null)
                return ServiceResult<bool>.Missing("Запись не найдена");

            entity.IsDeleted = true;
            entity.UpdatedAt = Now;
            entity.ServerModifiedAt = Now;
            await db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Success(true);
        }

        // ------------------------------------------------------------------ history

        public async Task<List<HistoryEntryDto>> GetHistoryAsync(int userId, int take, CancellationToken ct) =>
            (await db.HistoryEntries.AsNoTracking()
                .Where(h => h.UserId == userId && !h.IsDeleted)
                .OrderByDescending(h => h.ChangeDate)
                .Take(Math.Clamp(take, 1, 1000))
                .ToListAsync(ct))
            .Select(ToDto).ToList();

        public async Task ClearHistoryAsync(int userId, CancellationToken ct)
        {
            // Tombstones (not hard deletes) so that other devices learn about the clean-up.
            var entries = await db.HistoryEntries.Where(h => h.UserId == userId && !h.IsDeleted).ToListAsync(ct);
            foreach (var entry in entries)
            {
                entry.IsDeleted = true;
                entry.UpdatedAt = Now;
                entry.ServerModifiedAt = Now;
            }
            await db.SaveChangesAsync(ct);
        }

        // ------------------------------------------------------------------ sync

        /// <summary>
        /// Applies the device's changes (last write wins by UpdatedAt) and returns everything that changed on the
        /// server since <see cref="SyncRequest.Since"/>.
        /// </summary>
        public async Task<SyncResponse> SyncAsync(int userId, SyncRequest request, CancellationToken ct)
        {
            var now = Now;
            var response = new SyncResponse();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Categories first: items are validated against them.
            await UpsertAsync(db.ItemCategories, userId, request.Categories ?? new(), "category", response, now,
                validate: c =>
                {
                    c.Name = c.Name?.Trim() ?? string.Empty;
                    c.Kind = BuiltInCategories.KindOf(c.Id);
                    if (BuiltInCategories.IsBuiltIn(c.Id)) c.IsDeleted = false;
                    return c.IsDeleted ? null : ItemRules.ValidateCategory(c).FirstOrDefault();
                },
                create: () => new ItemCategory(), apply: Apply);
            await db.SaveChangesAsync(ct);

            var categories = (await GetCategoriesAsync(userId, ct)).ToDictionary(c => c.Id);
            var allCategoryIds = (await db.ItemCategories.Where(c => c.UserId == userId).Select(c => c.Id).ToListAsync(ct)).ToHashSet();

            await UpsertAsync(db.Items, userId, request.Items ?? new(), "item", response, now,
                validate: i =>
                {
                    // A tombstoned category is still known — the item simply follows it.
                    if (!categories.ContainsKey(i.CategoryId) && !allCategoryIds.Contains(i.CategoryId))
                        return "Неизвестная категория";
                    var category = categories.GetValueOrDefault(i.CategoryId);
                    ItemRules.Normalize(i, category);
                    return i.IsDeleted ? null : ItemRules.Validate(i, category).FirstOrDefault();
                },
                create: () => new Item(), apply: Apply);

            await UpsertAsync(db.ShoppingItems, userId, request.ShoppingItems ?? new(), "shopping", response, now,
                validate: s =>
                {
                    s.Name = s.Name?.Trim() ?? string.Empty;
                    return !s.IsDeleted && s.Name.Length == 0 ? "Название не может быть пустым" : null;
                },
                create: () => new ShoppingItem(), apply: Apply);

            await UpsertAsync(db.HistoryEntries, userId, request.History ?? new(), "history", response, now,
                validate: _ => null, create: () => new HistoryEntry(), apply: Apply);

            await db.SaveChangesAsync(ct);

            // Deleting a category on a device also deletes its items here.
            foreach (var deleted in (request.Categories ?? new()).Where(c => c.IsDeleted && !BuiltInCategories.IsBuiltIn(c.Id)))
                await DeleteItemsOfCategoryAsync(userId, deleted.Id, ct);
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            // Pull. Our own writes of this request are included; re-applying them on the device is harmless.
            var since = request.Since.HasValue ? Utc(request.Since.Value) : (DateTime?)null;
            response.Categories = (await Changed(db.ItemCategories, userId, since, ct)).Select(ToDto).ToList();
            response.Items = (await Changed(db.Items, userId, since, ct)).Select(ToDto).ToList();
            response.ShoppingItems = (await Changed(db.ShoppingItems, userId, since, ct)).Select(ToDto).ToList();
            response.History = (await Changed(db.HistoryEntries, userId, since, ct)).Select(ToDto).ToList();
            response.Cursor = now;
            return response;
        }

        private static async Task<List<T>> Changed<T>(DbSet<T> set, int userId, DateTime? since, CancellationToken ct)
            where T : class, IUserEntity
        {
            var query = set.AsNoTracking().Where(e => e.UserId == userId);
            query = since == null
                ? query.Where(e => !e.IsDeleted)              // first sync: no need for tombstones
                : query.Where(e => e.ServerModifiedAt > since);
            return await query.ToListAsync(ct);
        }

        private async Task UpsertAsync<TEntity, TDto>(
            DbSet<TEntity> set, int userId, List<TDto> incoming, string entityName, SyncResponse response, DateTime now,
            Func<TDto, string?> validate, Func<TEntity> create, Action<TDto, TEntity> apply)
            where TEntity : class, IUserEntity
            where TDto : class, ISyncEntity
        {
            // One copy per id: the newest.
            var latest = incoming
                .Where(d => d != null && d.Id != Guid.Empty)
                .GroupBy(d => d.Id)
                .Select(g => g.OrderByDescending(d => d.UpdatedAt).First())
                .ToList();
            if (latest.Count == 0)
                return;

            var ids = latest.Select(d => d.Id).ToList();
            var existing = await set.Where(e => e.UserId == userId && ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);

            foreach (var dto in latest)
            {
                // A device clock far in the future would make its edits impossible to override.
                dto.UpdatedAt = Utc(dto.UpdatedAt);
                if (dto.UpdatedAt > now.AddMinutes(5))
                    dto.UpdatedAt = now;

                var error = validate(dto);
                if (error != null)
                {
                    response.Rejected.Add(new SyncRejection { Id = dto.Id, Entity = entityName, Reason = error });
                    continue;
                }

                if (existing.TryGetValue(dto.Id, out var entity))
                {
                    if (!SyncRules.IncomingWins(dto.UpdatedAt, entity.UpdatedAt))
                        continue; // the server copy is newer and will be sent back in the pull
                }
                else
                {
                    entity = create();
                    entity.UserId = userId;
                    entity.Id = dto.Id;
                    set.Add(entity);
                }

                apply(dto, entity);
                entity.ServerModifiedAt = now;
            }
        }

        // ------------------------------------------------------------------ mapping

        internal static DateTime Utc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private static CategoryDto ToDto(ItemCategory c) => new()
        {
            Id = c.Id, Name = c.Name, Kind = c.Kind, Fields = c.Fields.Select(f => f.Clone()).ToList(),
            UpdatedAt = Utc(c.UpdatedAt), IsDeleted = c.IsDeleted
        };

        private static void Apply(CategoryDto d, ItemCategory c)
        {
            c.Name = d.Name;
            c.Kind = BuiltInCategories.KindOf(d.Id);
            c.Fields = (d.Fields ?? new()).OrderBy(f => f.Order).Select(f => f.Clone()).ToList();
            c.UpdatedAt = Utc(d.UpdatedAt);
            c.IsDeleted = d.IsDeleted;
        }

        private static ItemDto ToDto(Item i) => new()
        {
            Id = i.Id, CategoryId = i.CategoryId, Name = i.Name, Brand = i.Brand, Notes = i.Notes, Quantity = i.Quantity,
            Weight = i.Weight, WeightUnit = i.WeightUnit, ExpirationDate = i.ExpirationDate, Location = i.Location,
            Barcode = i.Barcode, Fields = new Dictionary<string, string>(i.Fields),
            CreatedAt = Utc(i.CreatedAt), UpdatedAt = Utc(i.UpdatedAt), IsDeleted = i.IsDeleted
        };

        private static void Apply(ItemDto d, Item i)
        {
            i.CategoryId = d.CategoryId;
            i.Name = d.Name;
            i.Brand = d.Brand;
            i.Notes = d.Notes;
            i.Quantity = Math.Max(0, d.Quantity);
            i.Weight = d.Weight;
            i.WeightUnit = d.WeightUnit;
            i.ExpirationDate = d.ExpirationDate;
            i.Location = d.Location;
            i.Barcode = d.Barcode;
            i.Fields = new Dictionary<string, string>(d.Fields ?? new());
            i.CreatedAt = d.CreatedAt == default ? Utc(d.UpdatedAt) : Utc(d.CreatedAt);
            i.UpdatedAt = Utc(d.UpdatedAt);
            i.IsDeleted = d.IsDeleted;
        }

        private static ShoppingItemDto ToDto(ShoppingItem s) => new()
        {
            Id = s.Id, Name = s.Name, Quantity = s.Quantity, IsChecked = s.IsChecked, ItemId = s.ItemId,
            CategoryId = s.CategoryId, CreatedAt = Utc(s.CreatedAt), UpdatedAt = Utc(s.UpdatedAt), IsDeleted = s.IsDeleted
        };

        private static void Apply(ShoppingItemDto d, ShoppingItem s)
        {
            s.Name = d.Name;
            s.Quantity = Math.Max(1, d.Quantity);
            s.IsChecked = d.IsChecked;
            s.ItemId = d.ItemId;
            s.CategoryId = d.CategoryId;
            s.CreatedAt = d.CreatedAt == default ? Utc(d.UpdatedAt) : Utc(d.CreatedAt);
            s.UpdatedAt = Utc(d.UpdatedAt);
            s.IsDeleted = d.IsDeleted;
        }

        private static HistoryEntryDto ToDto(HistoryEntry h) => new()
        {
            Id = h.Id, ItemId = h.ItemId, ItemName = h.ItemName, CategoryId = h.CategoryId, ChangeType = h.ChangeType,
            QuantityChange = h.QuantityChange, ChangeDate = Utc(h.ChangeDate), UpdatedAt = Utc(h.UpdatedAt), IsDeleted = h.IsDeleted
        };

        private static void Apply(HistoryEntryDto d, HistoryEntry h)
        {
            h.ItemId = d.ItemId;
            h.ItemName = d.ItemName ?? string.Empty;
            h.CategoryId = d.CategoryId;
            h.ChangeType = string.IsNullOrEmpty(d.ChangeType) ? ChangeTypes.Updated : d.ChangeType;
            h.QuantityChange = d.QuantityChange;
            h.ChangeDate = d.ChangeDate == default ? Utc(d.UpdatedAt) : Utc(d.ChangeDate);
            h.UpdatedAt = Utc(d.UpdatedAt);
            h.IsDeleted = d.IsDeleted;
        }
    }
}
