namespace PizzaOrderDesigner.Api.Pricing;

// ---- Request: what the customer is ordering (docs/API.md, "OrderDraft"). Nullable so validation can explain gaps.

public sealed record OrderDraft
{
    public IReadOnlyList<DraftLineInput>? Lines { get; init; }

    /// <summary>One promo code per order (D-043), or null.</summary>
    public string? PromoCode { get; init; }

    public TipInput? Tip { get; init; }
}

public sealed record DraftLineInput
{
    public string? SizeCode { get; init; }
    public IReadOnlyList<string>? ToppingCodes { get; init; }
    public int Quantity { get; init; }
}

/// <summary>A tip as "amount" (dollars) or "percent". <see cref="Value"/> is the customer's text, e.g. "2.50" or "15".</summary>
public sealed record TipInput
{
    public string? Mode { get; init; }
    public string? Value { get; init; }
}

// ---- Response: the priced order (docs/API.md, "PriceBreakdown").

public sealed record PriceBreakdown(
    IReadOnlyList<PricedLine> Lines,
    decimal Subtotal,
    string? PromoCode,
    decimal Discount,
    decimal TaxRate,
    decimal Tax,
    string TipBase,
    decimal Tip,
    decimal Total,
    string Description);

public sealed record PricedLine(
    string SizeCode,
    string SizeName,
    int Quantity,
    decimal BasePrice,
    IReadOnlyList<PricedTopping> Toppings,
    decimal UnitPrice,
    decimal LineTotal,
    string Description);

public sealed record PricedTopping(string Code, string Name, string CategoryCode, decimal Price);

/// <summary>Either a breakdown or field errors (field path → customer-facing messages).</summary>
public sealed record PricingResult(PriceBreakdown? Breakdown, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Breakdown is not null;
}
