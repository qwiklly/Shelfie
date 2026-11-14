using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelfie.Shared;
using ShelfieBackend.Models;
using System.Text.Json;

namespace ShelfieBackend.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<ItemCategory> ItemCategories { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ShoppingItem> ShoppingItems { get; set; }
        public DbSet<HistoryEntry> HistoryEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // PostgreSQL stores JSON as jsonb; other providers (SQLite in tests) as text.
            string? jsonType = Database.IsNpgsql() ? "jsonb" : null;

            modelBuilder.Entity<ItemCategory>(e =>
            {
                ConfigureUserEntity(e);
                e.Property(c => c.Name).HasMaxLength(100);
                e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
                e.Property(c => c.Fields)
                    .HasConversion(Json<List<FieldDefinition>>())
                    .Metadata.SetValueComparer(JsonComparer<List<FieldDefinition>>());
                if (jsonType != null) e.Property(c => c.Fields).HasColumnType(jsonType);
            });

            modelBuilder.Entity<Item>(e =>
            {
                ConfigureUserEntity(e);
                e.Property(i => i.Name).HasMaxLength(ItemRules.MaxNameLength);
                e.Property(i => i.Fields)
                    .HasConversion(Json<Dictionary<string, string>>())
                    .Metadata.SetValueComparer(JsonComparer<Dictionary<string, string>>());
                if (jsonType != null) e.Property(i => i.Fields).HasColumnType(jsonType);
                e.HasIndex(i => new { i.UserId, i.Barcode });
            });

            modelBuilder.Entity<ShoppingItem>(ConfigureUserEntity);

            modelBuilder.Entity<HistoryEntry>(e =>
            {
                ConfigureUserEntity(e);
                e.Property(h => h.ChangeType).HasMaxLength(20);
                e.HasIndex(h => new { h.UserId, h.ChangeDate });
            });
        }

        private static void ConfigureUserEntity<T>(EntityTypeBuilder<T> e) where T : class, IUserEntity
        {
            e.HasKey(x => new { x.UserId, x.Id });
            e.HasIndex(x => new { x.UserId, x.ServerModifiedAt });
            // Deleting a user removes all of their data.
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        }

        private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> Json<T>() where T : new() =>
            new(v => JsonSerializer.Serialize(v, ShelfieJson.Options),
                v => string.IsNullOrEmpty(v) ? new T() : JsonSerializer.Deserialize<T>(v, ShelfieJson.Options) ?? new T());

        private static ValueComparer<T> JsonComparer<T>() where T : new() =>
            new((a, b) => JsonSerializer.Serialize(a, ShelfieJson.Options) == JsonSerializer.Serialize(b, ShelfieJson.Options),
                v => JsonSerializer.Serialize(v, ShelfieJson.Options).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, ShelfieJson.Options), ShelfieJson.Options) ?? new T());
    }
}
