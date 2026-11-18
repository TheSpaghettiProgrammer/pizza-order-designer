namespace PizzaOrderDesigner.Data.Orders;

/// <summary>The brief's four statuses, in order. There is no Cancelled: cancelling deletes the order (D-040).</summary>
public enum OrderStatus
{
    NotStarted,
    Started,
    ReadyForPickup,
    Delivered,
}

public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // What the customer asked for (re-priced on update).
    public string? PromoCode { get; set; }
    public string? TipMode { get; set; }
    public string? TipValue { get; set; }
    public List<OrderLine> Lines { get; set; } = [];

    // Priced snapshot at submit/update, so a later menu change never alters a placed order.
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }

    /// <summary>The full price breakdown as JSON (jsonb), returned as-is by GET.</summary>
    public required string PricingJson { get; set; }

    /// <summary>Postgres xmin: changes on every write. Used for optimistic concurrency and as the ETag.</summary>
    public uint Version { get; set; }
}

public class OrderLine
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int Position { get; set; }
    public required string SizeCode { get; set; }
    public int Quantity { get; set; }
    public List<string> ToppingCodes { get; set; } = [];
}

/// <summary>
/// One row per Idempotency-Key. No foreign key to orders: the record deliberately outlives a deleted (cancelled)
/// order, so a late retry of the original submit can't re-create it (D-040).
/// </summary>
public class IdempotencyRecord
{
    public required string Key { get; set; }
    public required string RequestHash { get; set; }
    public Guid OrderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
