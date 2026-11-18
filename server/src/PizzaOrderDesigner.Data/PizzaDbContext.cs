using Microsoft.EntityFrameworkCore;

namespace PizzaOrderDesigner.Data;

/// <summary>
/// EF Core context for the pizza order designer. Entities are added as the domain is defined;
/// each entity's mapping lives in its own IEntityTypeConfiguration class in this assembly.
/// </summary>
public class PizzaDbContext(DbContextOptions<PizzaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PizzaDbContext).Assembly);
    }
}
