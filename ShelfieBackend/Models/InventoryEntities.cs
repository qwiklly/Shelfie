using Shelfie.Shared;

namespace ShelfieBackend.Models
{
    /// <summary>
    /// Server copy of a synchronised entity. Rows are keyed by (UserId, Id), so ids generated on devices
    /// can never collide with — or overwrite — data of another user.
    /// </summary>
    public interface IUserEntity
    {
        int UserId { get; set; }
        Guid Id { get; set; }
        DateTime UpdatedAt { get; set; }

        /// <summary>Server clock time of the last write; used as the sync cursor.</summary>
        DateTime ServerModifiedAt { get; set; }
        bool IsDeleted { get; set; }
    }

    /// <summary>A custom category, or the user's override of a built-in one (same well-known id).</summary>
    public class ItemCategory : IUserEntity
    {
        public int UserId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CategoryKind Kind { get; set; } = CategoryKind.Custom;
        public List<FieldDefinition> Fields { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class Item : IUserEntity
    {
        public int UserId { get; set; }
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Notes { get; set; }
        public int Quantity { get; set; }
        public double? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public string? Location { get; set; }
        public string? Barcode { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ShoppingItem : IUserEntity
    {
        public int UserId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsChecked { get; set; }
        public Guid? ItemId { get; set; }
        public Guid? CategoryId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class HistoryEntry : IUserEntity
    {
        public int UserId { get; set; }
        public Guid Id { get; set; }
        public Guid? ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public DateTime ChangeDate { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
