using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PromoCode).HasMaxLength(50);
        b.Property(x => x.TipMode).HasMaxLength(10);
        b.Property(x => x.TipValue).HasMaxLength(20);
        foreach (var money in new[] { nameof(Order.Subtotal), nameof(Order.Discount), nameof(Order.Tax), nameof(Order.Tip), nameof(Order.Total) })
        {
            b.Property<decimal>(money).HasPrecision(12, 2);
        }

        b.Property(x => x.PricingJson).HasColumnType("jsonb");
        b.Property(x => x.Version).IsRowVersion(); // maps to Postgres xmin
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.UpdatedAt });
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> b)
    {
        b.ToTable("order_lines");
        b.Property(x => x.SizeCode).HasMaxLength(50);
    }
}

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.RequestHash).HasMaxLength(64);
    }
}
