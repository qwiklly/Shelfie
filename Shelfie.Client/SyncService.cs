using Shelfie.Client.Storage;
using Shelfie.Shared;

namespace Shelfie.Client;

public enum SyncState
{
    /// <summary>Guest mode or the session expired: data stays on the device.</summary>
    NotSignedIn,
    Offline,
    Syncing,
    Synced,
    Error
}

public sealed record SyncStatus(SyncState State, DateTime? LastSyncUtc, int PendingChanges, string? Message = null);

/// <summary>
/// Two-way synchronisation of <see cref="LocalInventory"/> with the server.
/// Push: every dirty row. Pull: everything changed on the server since the stored cursor.
/// Conflicts: last write (UpdatedAt) wins on both sides.
/// </summary>
public sealed class SyncService
{
    private const string CursorKey = "sync.cursor";
    private const string LastSyncKey = "sync.last";

    private readonly LocalInventory _inventory;
    private readonly ShelfieApi _api;
    private readonly SemaphoreSlim _running = new(1, 1);

    public SyncService(LocalInventory inventory, ShelfieApi api)
    {
        _inventory = inventory;
        _api = api;
    }

    public SyncStatus Status { get; private set; } = new(SyncState.NotSignedIn, null, 0);

    public event EventHandler<SyncStatus>? StatusChanged;

    private LocalDatabase Db => _inventory.Database;

    private async Task<SyncStatus> SetStatusAsync(SyncState state, string? message = null)
    {
        var last = await Db.GetMetaAsync(LastSyncKey);
        DateTime? lastSync = long.TryParse(last, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null;
        Status = new SyncStatus(state, lastSync, await _inventory.CountPendingChangesAsync(), message);
        StatusChanged?.Invoke(this, Status);
        return Status;
    }

    public async Task<SyncStatus> RefreshStatusAsync()
    {
        bool signedIn = await _api.GetValidTokenAsync() != null;
        var state = !signedIn ? SyncState.NotSignedIn
            : Status.State == SyncState.NotSignedIn ? SyncState.Offline
            : Status.State;
        return await SetStatusAsync(state, Status.Message);
    }

    /// <summary>Forget the server position so the next sync downloads everything (used for a new account).</summary>
    public async Task ResetAsync()
    {
        await Db.EnsureCreatedAsync();
        await Db.SetMetaAsync(CursorKey, null);
        await Db.SetMetaAsync(LastSyncKey, null);
    }

    public async Task<SyncStatus> SyncAsync(CancellationToken ct = default)
    {
        await Db.EnsureCreatedAsync();
        if (await _api.GetValidTokenAsync() == null)
            return await SetStatusAsync(SyncState.NotSignedIn);

        if (!await _running.WaitAsync(0, ct))
            return Status; // a sync is already running

        try
        {
            await SetStatusAsync(SyncState.Syncing);

            var (request, sent) = await CollectChangesAsync();
            var result = await _api.SyncAsync(request, ct);
            if (!result.Success)
            {
                if (result.IsOffline)
                    return await SetStatusAsync(SyncState.Offline, result.Error);
                if (result.Status == System.Net.HttpStatusCode.Unauthorized)
                    return await SetStatusAsync(SyncState.NotSignedIn, Texts.Get("SessionExpired"));
                return await SetStatusAsync(SyncState.Error, result.Error);
            }

            bool changed = await ApplyAsync(result.Value!, sent);
            await Db.SetMetaAsync(CursorKey, result.Value!.Cursor.Ticks.ToString());
            await Db.SetMetaAsync(LastSyncKey, DateTime.UtcNow.Ticks.ToString());

            if (changed)
                _inventory.RaiseChanged();

            var rejected = result.Value!.Rejected.Count;
            return await SetStatusAsync(SyncState.Synced, rejected > 0 ? Texts.Format("ServerRejected", rejected) : null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return await SetStatusAsync(SyncState.Offline);
        }
        catch (Exception ex)
        {
            return await SetStatusAsync(SyncState.Error, ex.Message);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>Dirty rows and the UpdatedAt each had when it was sent.</summary>
    private async Task<(SyncRequest, Dictionary<Guid, DateTime>)> CollectChangesAsync()
    {
        var c = Db.Connection;
        var cursor = await Db.GetMetaAsync(CursorKey);
        var request = new SyncRequest
        {
            Since = long.TryParse(cursor, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null,
            Categories = (await c.Table<CategoryRow>().Where(r => r.IsDirty).ToListAsync()).Select(r => r.ToDto()).ToList(),
            Items = (await c.Table<ItemRow>().Where(r => r.IsDirty).ToListAsync()).Select(r => r.ToDto()).ToList(),
            ShoppingItems = (await c.Table<ShoppingRow>().Where(r => r.IsDirty).ToListAsync()).Select(r => r.ToDto()).ToList(),
            History = (await c.Table<HistoryRow>().Where(r => r.IsDirty).ToListAsync()).Select(r => r.ToDto()).ToList(),
        };

        var sent = new Dictionary<Guid, DateTime>();
        foreach (var e in request.Categories.Cast<ISyncEntity>().Concat(request.Items).Concat(request.ShoppingItems).Concat(request.History))
            sent[e.Id] = e.UpdatedAt;
        return (request, sent);
    }

    private async Task<bool> ApplyAsync(SyncResponse response, Dictionary<Guid, DateTime> sent)
    {
        await _inventory.Lock.WaitAsync();
        try
        {
            bool changed = false;
            changed |= await ApplyTableAsync<CategoryRow, CategoryDto>(response.Categories, sent, r => r.Id, r => r.UpdatedAt, r => r.IsDirty, (r, v) => r.IsDirty = v, (r, d) => r.Apply(d), r => r.IsDeleted);
            changed |= await ApplyTableAsync<ItemRow, ItemDto>(response.Items, sent, r => r.Id, r => r.UpdatedAt, r => r.IsDirty, (r, v) => r.IsDirty = v, (r, d) => r.Apply(d), r => r.IsDeleted);
            changed |= await ApplyTableAsync<ShoppingRow, ShoppingItemDto>(response.ShoppingItems, sent, r => r.Id, r => r.UpdatedAt, r => r.IsDirty, (r, v) => r.IsDirty = v, (r, d) => r.Apply(d), r => r.IsDeleted);
            changed |= await ApplyTableAsync<HistoryRow, HistoryEntryDto>(response.History, sent, r => r.Id, r => r.UpdatedAt, r => r.IsDirty, (r, v) => r.IsDirty = v, (r, d) => r.Apply(d), r => r.IsDeleted);
            return changed;
        }
        finally
        {
            _inventory.Lock.Release();
        }
    }

    private async Task<bool> ApplyTableAsync<TRow, TDto>(
        List<TDto> incoming, Dictionary<Guid, DateTime> sent,
        Func<TRow, Guid> id, Func<TRow, DateTime> updatedAt, Func<TRow, bool> isDirty, Action<TRow, bool> setDirty,
        Action<TRow, TDto> apply, Func<TRow, bool> isDeleted)
        where TRow : new()
        where TDto : ISyncEntity
    {
        var c = Db.Connection;
        var rows = (await c.Table<TRow>().ToListAsync()).ToDictionary(id);
        bool changed = false;

        // 1. What we sent is now on the server — unless it was edited again while the request was in flight.
        foreach (var row in rows.Values)
        {
            if (isDirty(row) && sent.TryGetValue(id(row), out var sentAt) && Mapping.Utc(updatedAt(row)) == Mapping.Utc(sentAt))
            {
                setDirty(row, false);
                await c.UpdateAsync(row);
            }
        }

        // 2. Server changes, last write wins.
        foreach (var dto in incoming)
        {
            if (rows.TryGetValue(dto.Id, out var row))
            {
                if (isDirty(row) && Mapping.Utc(updatedAt(row)) > Mapping.Utc(dto.UpdatedAt))
                    continue; // newer local edit, it goes up next time

                if (!SyncRules.IncomingWins(Mapping.Utc(dto.UpdatedAt), Mapping.Utc(updatedAt(row))) && !isDirty(row))
                    continue;

                apply(row, dto);
                setDirty(row, false);
                await c.UpdateAsync(row);
                changed = true;
            }
            else if (!dto.IsDeleted)
            {
                var created = new TRow();
                apply(created, dto);
                setDirty(created, false);
                await c.InsertAsync(created);
                rows[dto.Id] = created;
                changed = true;
            }
        }

        // 3. Tombstones the server has acknowledged are no longer needed on the device.
        foreach (var row in rows.Values.Where(r => isDeleted(r) && !isDirty(r)))
            await c.DeleteAsync(row);

        return changed;
    }
}
