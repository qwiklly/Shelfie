namespace Shelfie.Shared;

public enum CategoryKind
{
    Products,
    Medications,
    Books,
    Custom
}

public enum FieldType
{
    Text,
    Number,
    Date,
    Boolean
}

/// <summary>
/// An extra field of a category. <see cref="Key"/> is stable: renaming or reordering a field keeps its values.
/// </summary>
public sealed class FieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public bool Required { get; set; }
    public int Order { get; set; }

    public FieldDefinition Clone() => (FieldDefinition)MemberwiseClone();
}

/// <summary>Common shape of everything that is synchronised between devices and the server.</summary>
public interface ISyncEntity
{
    Guid Id { get; set; }

    /// <summary>When the entity was last changed (UTC, clock of the device that changed it). Last write wins.</summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>Tombstone: deletions are synchronised like any other change.</summary>
    bool IsDeleted { get; set; }
}

public sealed class CategoryDto : ISyncEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Custom;
    public List<FieldDefinition> Fields { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ItemDto : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Notes { get; set; }
    public int Quantity { get; set; } = 1;
    public double? Weight { get; set; }
    public string? WeightUnit { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Location { get; set; }
    public string? Barcode { get; set; }

    /// <summary>Values of the category's extra fields, by <see cref="FieldDefinition.Key"/>.</summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ShoppingItemDto : ISyncEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsChecked { get; set; }

    /// <summary>The inventory item this entry restocks, if any.</summary>
    public Guid? ItemId { get; set; }
    public Guid? CategoryId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public static class ChangeTypes
{
    public const string Added = "Added";
    public const string Updated = "Updated";
    public const string Removed = "Removed";
    public const string Consumed = "Consumed";
    public const string Wasted = "Wasted";
    public const string Restocked = "Restocked";
}

public sealed class HistoryEntryDto : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string ChangeType { get; set; } = ChangeTypes.Updated;
    public int QuantityChange { get; set; }
    public DateTime ChangeDate { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class SyncRequest
{
    /// <summary>Cursor returned by the previous sync; null on the first sync.</summary>
    public DateTime? Since { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
    public List<ItemDto> Items { get; set; } = new();
    public List<ShoppingItemDto> ShoppingItems { get; set; } = new();
    public List<HistoryEntryDto> History { get; set; } = new();
}

public sealed class SyncRejection
{
    public Guid Id { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class SyncResponse
{
    /// <summary>Pass this as <see cref="SyncRequest.Since"/> next time.</summary>
    public DateTime Cursor { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
    public List<ItemDto> Items { get; set; } = new();
    public List<ShoppingItemDto> ShoppingItems { get; set; } = new();
    public List<HistoryEntryDto> History { get; set; } = new();
    public List<SyncRejection> Rejected { get; set; } = new();
}

/// <summary>Standard answer of command endpoints.</summary>
public record ApiMessage(bool Flag = false, string Message = "");
