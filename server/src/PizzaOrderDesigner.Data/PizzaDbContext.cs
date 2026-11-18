using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Data.Menu;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Data;

/// <summary>
/// EF Core context for the pizza order designer. Each entity's mapping lives in its own
/// IEntityTypeConfiguration class in this assembly.
/// </summary>
public class PizzaDbContext(DbContextOptions<PizzaDbContext> options) : DbContext(options)
{
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<ToppingCategory> ToppingCategories => Set<ToppingCategory>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PizzaDbContext).Assembly);
    }
}
