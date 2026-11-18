using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzaOrderDesigner.Data.Menu;

namespace PizzaOrderDesigner.Data.Configurations;

public class SizeConfiguration : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> b)
    {
        b.ToTable("sizes");
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.BasePrice).HasPrecision(12, 2);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(MenuSeed.Sizes);
    }
}

public class ToppingCategoryConfiguration : IEntityTypeConfiguration<ToppingCategory>
{
    public void Configure(EntityTypeBuilder<ToppingCategory> b)
    {
        b.ToTable("topping_categories");
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Rate).HasPrecision(7, 4);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(MenuSeed.Categories.Select(c => new { c.Id, c.Code, c.Name, c.Rate, c.SortOrder }));
    }
}

public class ToppingConfiguration : IEntityTypeConfiguration<Topping>
{
    public void Configure(EntityTypeBuilder<Topping> b)
    {
        b.ToTable("toppings");
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne(x => x.Category).WithMany(c => c.Toppings).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(MenuSeed.Toppings);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("promotions");
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.RuleType).HasMaxLength(50);
        b.Property(x => x.ExclusionGroup).HasMaxLength(50);
        b.Property(x => x.Percent).HasPrecision(7, 4);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.MinSubtotalExclusive).HasPrecision(12, 2);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(MenuSeed.Promotions);
    }
}
