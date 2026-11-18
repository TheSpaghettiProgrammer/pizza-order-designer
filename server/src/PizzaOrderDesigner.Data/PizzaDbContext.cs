using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Data.Menu;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PizzaDbContext).Assembly);
    }
}
