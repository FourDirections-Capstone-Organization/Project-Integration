using Microsoft.EntityFrameworkCore;
using DeliverySystem.Api.Models.Entities;

namespace DeliverySystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
}
