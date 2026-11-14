using System.Text.Json;
using Shelfie.Shared;

namespace Shelfie.Client.Storage;

internal static class Mapping
{
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static T FromJson<T>(string? json) where T : new() =>
        string.IsNullOrEmpty(json) ? new T() : JsonSerializer.Deserialize<T>(json, ShelfieJson.Options) ?? new T();

    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value, ShelfieJson.Options);

    public static CategoryDto ToDto(this CategoryRow r) => new()
    {
        Id = r.Id, Name = r.Name, Kind = (CategoryKind)r.Kind, Fields = FromJson<List<FieldDefinition>>(r.FieldsJson),
        UpdatedAt = Utc(r.UpdatedAt), IsDeleted = r.IsDeleted
    };

    public static void Apply(this CategoryRow r, CategoryDto d)
    {
        r.Id = d.Id;
        r.Name = d.Name;
        r.Kind = (int)BuiltInCategories.KindOf(d.Id);
        r.FieldsJson = ToJson((d.Fields ?? new()).OrderBy(f => f.Order).ToList());
        r.UpdatedAt = Utc(d.UpdatedAt);
        r.IsDeleted = d.IsDeleted;
    }

    public static ItemDto ToDto(this ItemRow r) => new()
    {
        Id = r.Id, CategoryId = r.CategoryId, Name = r.Name, Brand = r.Brand, Notes = r.Notes, Quantity = r.Quantity,
        Weight = r.Weight, WeightUnit = r.WeightUnit,
        ExpirationDate = r.ExpirationDay is int day ? DateOnly.FromDayNumber(day) : null,
        Location = r.Location, Barcode = r.Barcode, Fields = FromJson<Dictionary<string, string>>(r.FieldsJson),
        CreatedAt = Utc(r.CreatedAt), UpdatedAt = Utc(r.UpdatedAt), IsDeleted = r.IsDeleted
    };

    /// <summary>Copies synchronised fields; the device-only photo path is kept.</summary>
    public static void Apply(this ItemRow r, ItemDto d)
    {
        r.Id = d.Id;
        r.CategoryId = d.CategoryId;
        r.Name = d.Name;
        r.Brand = d.Brand;
        r.Notes = d.Notes;
        r.Quantity = Math.Max(0, d.Quantity);
        r.Weight = d.Weight;
        r.WeightUnit = d.WeightUnit;
        r.ExpirationDay = d.ExpirationDate?.DayNumber;
        r.Location = d.Location;
        r.Barcode = d.Barcode;
        r.FieldsJson = ToJson(d.Fields ?? new());
        r.CreatedAt = Utc(d.CreatedAt == default ? d.UpdatedAt : d.CreatedAt);
        r.UpdatedAt = Utc(d.UpdatedAt);
        r.IsDeleted = d.IsDeleted;
    }

    public static ShoppingItemDto ToDto(this ShoppingRow r) => new()
    {
        Id = r.Id, Name = r.Name, Quantity = r.Quantity, IsChecked = r.IsChecked, ItemId = r.ItemId,
        CategoryId = r.CategoryId, CreatedAt = Utc(r.CreatedAt), UpdatedAt = Utc(r.UpdatedAt), IsDeleted = r.IsDeleted
    };

    public static void Apply(this ShoppingRow r, ShoppingItemDto d)
    {
        r.Id = d.Id;
        r.Name = d.Name;
        r.Quantity = Math.Max(1, d.Quantity);
        r.IsChecked = d.IsChecked;
        r.ItemId = d.ItemId;
        r.CategoryId = d.CategoryId;
        r.CreatedAt = Utc(d.CreatedAt == default ? d.UpdatedAt : d.CreatedAt);
        r.UpdatedAt = Utc(d.UpdatedAt);
        r.IsDeleted = d.IsDeleted;
    }

    public static HistoryEntryDto ToDto(this HistoryRow r) => new()
    {
        Id = r.Id, ItemId = r.ItemId, ItemName = r.ItemName, CategoryId = r.CategoryId, ChangeType = r.ChangeType,
        QuantityChange = r.QuantityChange, ChangeDate = Utc(r.ChangeDate), UpdatedAt = Utc(r.UpdatedAt), IsDeleted = r.IsDeleted
    };

    public static void Apply(this HistoryRow r, HistoryEntryDto d)
    {
        r.Id = d.Id;
        r.ItemId = d.ItemId;
        r.ItemName = d.ItemName;
        r.CategoryId = d.CategoryId;
        r.ChangeType = d.ChangeType;
        r.QuantityChange = d.QuantityChange;
        r.ChangeDate = Utc(d.ChangeDate == default ? d.UpdatedAt : d.ChangeDate);
        r.UpdatedAt = Utc(d.UpdatedAt);
        r.IsDeleted = d.IsDeleted;
    }
}
