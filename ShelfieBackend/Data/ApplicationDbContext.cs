using Microsoft.EntityFrameworkCore;
using ShelfieBackend.Models;

namespace ShelfieBackend.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<HistoryRecord> HistoryRecords { get; set; }
        public DbSet<CategoryField> CategoryFields { get; set; }
        public DbSet<CategoryFieldValue> CategoryFieldValues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // -------------------------------
            // Связь ApplicationUser -> Category
            // Если категория привязана к пользователю (UserId не null),
            // то при удалении пользователя удаляются его категории.
            modelBuilder.Entity<Category>()
                .HasOne(c => c.User)
                .WithMany() // можно добавить коллекцию Categories в ApplicationUser для навигации
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.User)
                .WithMany() // можно добавить коллекцию Products в ApplicationUser
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> HistoryRecord
            modelBuilder.Entity<HistoryRecord>()
                .HasOne(hr => hr.User)
                .WithMany() // можно добавить коллекцию HistoryRecords в ApplicationUser
                .HasForeignKey(hr => hr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> CustomItem
            modelBuilder.Entity<CustomItem>()
                .HasOne(ci => ci.User)
                .WithMany() // можно добавить коллекцию CustomItems в ApplicationUser
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> Product
            // При удалении категории удаляются все продукты, привязанные к ней.
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany() // если в Category добавить коллекцию Products
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> CategoryField
            // При удалении категории удаляются все её поля.
            modelBuilder.Entity<CategoryField>()
                .HasOne<Category>()
                .WithMany() // можно добавить коллекцию CategoryFields в Category
                .HasForeignKey(cf => cf.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> CustomItem
            // При удалении категории удаляются и связанные кастомные элементы.
            modelBuilder.Entity<CustomItem>()
                .HasOne(ci => ci.Category)
                .WithMany() // можно добавить коллекцию CustomItems в Category
                .HasForeignKey(ci => ci.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Если потребуется настроить каскадное удаление для Medications или других сущностей,
            // можно добавить аналогичные настройки.
        }
    }
}