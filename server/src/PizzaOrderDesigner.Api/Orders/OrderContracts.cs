using System.Text.Json;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Orders;

/// <summary>Body for submit and update: the draft plus the total the customer saw (D-039).</summary>
public sealed record OrderRequest
{
    public IReadOnlyList<DraftLineInput>? Lines { get; init; }
    public string? PromoCode { get; init; }
    public TipInput? Tip { get; init; }

    /// <summary>The total shown to the customer. If the server's price differs, the request gets 409 price.changed.</summary>
    public decimal? ExpectedTotal { get; init; }

    public OrderDraft ToDraft() => new() { Lines = Lines, PromoCode = PromoCode, Tip = Tip };
}

/// <summary>An order as returned by the API (docs/API.md, "Order").</summary>
public sealed record OrderResponse(
    Guid Id,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version,
    JsonElement Pricing)
{
    public static OrderResponse From(Order order)
    {
        using var pricing = JsonDocument.Parse(order.PricingJson);
        return new OrderResponse(order.Id, order.Status, order.CreatedAt, order.UpdatedAt,
            order.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), pricing.RootElement.Clone());
    }
}

/// <summary>The outcome of an order operation: the order (on success) or a problem.</summary>
public sealed record OrderResult(Order? Order, OrderProblem? Problem, bool Created = false)
{
    public static OrderResult Ok(Order order, bool created = false) => new(order, null, created);

    public static OrderResult Fail(OrderProblem problem) => new(null, problem);
}

/// <summary>Kitchen simulation settings (D-042), bound from "Kitchen:AutoAdvance".</summary>
public sealed class KitchenAutoAdvanceOptions
{
    public const string SectionName = "Kitchen:AutoAdvance";

    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 20;
}
