using Microsoft.Extensions.Options;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Api.Pricing.Promotions;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

internal static class PricingTestSupport
{
    public static OrderPricer Pricer(TipBase tipBase = TipBase.PreTax) =>
        new(new PromotionEngine([new PercentOffOrderRule(), new AmountOffOverThresholdRule()]),
            Options.Create(new PricingOptions { TipBase = tipBase }));

    public static DraftLineInput Line(string size, int quantity = 1, params string[] toppings) =>
        new() { SizeCode = size, Quantity = quantity, ToppingCodes = toppings };

    public static OrderDraft Draft(IEnumerable<DraftLineInput> lines, string? promo = null, TipInput? tip = null) =>
        new() { Lines = lines.ToList(), PromoCode = promo, Tip = tip };

    public static TipInput Percent(string value) => new() { Mode = "percent", Value = value };

    public static TipInput Amount(string value) => new() { Mode = "amount", Value = value };

    public static PriceBreakdown Priced(this PricingResult result)
    {
        Assert.True(result.IsValid, "Expected a valid price, got errors: " +
            string.Join("; ", result.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
        return result.Breakdown!;
    }
}
