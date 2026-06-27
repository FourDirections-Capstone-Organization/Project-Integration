using Microsoft.EntityFrameworkCore;
using AuthService.Api.Models.Entities;

namespace AuthService.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasIndex(e => e.EmployeeNumber).IsUnique();
        });

        modelBuilder.Entity<UserAccount>().HasData(
            new UserAccount
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                EmployeeNumber = "admin",
                Name = "System Admin",
                Email = "admin@system.com",
                Role = "SystemAdmin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserAccount
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                EmployeeNumber = "SVC-OPERATIONAL",
                Name = "Operational System Service Account",
                Email = "svc-operational@system.com",
                Role = "Operational.ExternalService",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("svc-operational-pwd"),
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserAccount
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                EmployeeNumber = "SVC-DELIVERY",
                Name = "Delivery System Service Account",
                Email = "svc-delivery@system.com",
                Role = "Delivery.ExternalService",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("svc-delivery-pwd"),
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
