using Microsoft.EntityFrameworkCore;
using OperationalSystem.Api.Models.Entities;

namespace OperationalSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
}
