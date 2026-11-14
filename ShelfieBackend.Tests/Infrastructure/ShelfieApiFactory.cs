using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShelfieBackend.Data;
using ShelfieBackend.Models;
using ShelfieBackend.States;

namespace ShelfieBackend.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory with SQLite instead of PostgreSQL.
/// The three predefined categories of the initial migration are seeded with ids 1..3.
/// </summary>
public sealed class ShelfieApiFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "test-signing-key-that-is-long-enough-for-hs256!";
    public const string AdminEmail = "admin@shelfie.test";
    public const string AdminPassword = "Admin123";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ShelfieApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", "https://shelfie.test");
        builder.UseSetting("Jwt:Audience", "https://shelfie.test");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");

        builder.ConfigureServices(services =>
        {
            // Drop the Npgsql registration and use SQLite.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        if (!db.Categories.Any())
        {
            db.Categories.AddRange(
                new Category { Id = Constants.ProductsCategoryId, Name = "Продукты", Type = "Product" },
                new Category { Id = Constants.MedicationsCategoryId, Name = "Медикаменты", Type = "Medication" },
                new Category { Id = 3, Name = "Книги", Type = "Book" });

            db.Users.Add(new ApplicationUser
            {
                Name = "Admin",
                Email = AdminEmail,
                Role = UserRole.Admin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
                DateOfBirth = new DateOnly(1990, 1, 1)
            });
            db.SaveChanges();
        }

        return host;
    }

    /// <summary>Runs <paramref name="action"/> against the database of the running API.</summary>
    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
