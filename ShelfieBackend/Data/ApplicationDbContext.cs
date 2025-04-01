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
            modelBuilder.Entity<Category>()
                .HasOne(c => c.User)
                .WithMany() 
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.User)
                .WithMany() 
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> HistoryRecord
            modelBuilder.Entity<HistoryRecord>()
                .HasOne(hr => hr.User)
                .WithMany() 
                .HasForeignKey(hr => hr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь ApplicationUser -> CustomItem
            modelBuilder.Entity<CustomItem>()
                .HasOne(ci => ci.User)
                .WithMany() 
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> Product
            // При удалении категории удаляются все продукты, привязанные к ней.
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany() 
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> CategoryField
            // При удалении категории удаляются все её поля.
            modelBuilder.Entity<CategoryField>()
                .HasOne<Category>()
                .WithMany() 
                .HasForeignKey(cf => cf.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------------------
            // Связь Category -> CustomItem
            // При удалении категории удаляются и связанные кастомные элементы.
            modelBuilder.Entity<CustomItem>()
                .HasOne(ci => ci.Category)
                .WithMany() 
                .HasForeignKey(ci => ci.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}