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
    }
}