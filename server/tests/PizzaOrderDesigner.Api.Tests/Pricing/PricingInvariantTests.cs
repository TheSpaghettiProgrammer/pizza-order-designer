using PizzaOrderDesigner.Api.Pricing;
using static PizzaOrderDesigner.Api.Tests.Pricing.PricingTestSupport;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

/// <summary>
/// Invariants that must hold for every valid order, checked over many generated orders (fixed seed, so failures are
/// reproducible). These are the properties the brief grades: exact arithmetic, consistent rounding, one source of
/// truth for the total.
/// </summary>
public class PricingInvariantTests
{
    private const int Orders = 500;
    private readonly MenuSnapshot menu = TestMenu.FromSeed();

    public static TheoryData<TipBase> TipBases => [TipBase.PreTax, TipBase.PostTax];

    [Theory]
    [MemberData(nameof(TipBases))]
    public void Every_valid_order_satisfies_the_pricing_invariants(TipBase tipBase)
    {
        var pricer = Pricer(tipBase);
        var random = new Random(20261117);
        var priced = 0;

        for (var i = 0; i < Orders; i++)
        {
            var draft = RandomDraft(random);
            var result = pricer.Price(draft, menu);
            if (!result.IsValid)
            {
                // Only an ineligible SAVE5 can make a generated order invalid.
                Assert.Equal(["promoCode"], result.Errors.Keys);
                continue;
            }

            var b = result.Breakdown!;
            priced++;

            // 1. Every amount is whole cents.
            foreach (var amount in Amounts(b))
            {
                Assert.True(Money.HasAtMostTwoDecimals(amount), $"order {i}: {amount} has more than 2 decimals");
            }

            // 2. The total is exactly the sum of its displayed parts (what the customer sees adds up).
            Assert.Equal(b.Subtotal - b.Discount + b.Tax + b.Tip, b.Total);
            Assert.Equal(b.Lines.Sum(l => l.LineTotal), b.Subtotal);
            foreach (var line in b.Lines)
            {
                Assert.Equal(line.BasePrice + line.Toppings.Sum(t => t.Price), line.UnitPrice);
                Assert.Equal(line.UnitPrice * line.Quantity, line.LineTotal);
            }

            // 3. Discounts never exceed what they discount; nothing is negative.
            Assert.InRange(b.Discount, 0m, b.Subtotal);
            Assert.True(b.Total >= 0m && b.Tax >= 0m && b.Tip >= 0m);

            // 4. Tax is on the discounted amount, rounded once; the tip is never taxed.
            Assert.Equal(Money.RoundToCents((b.Subtotal - b.Discount) * b.TaxRate), b.Tax);

            // 5. One source of truth: pricing the same draft again gives the identical breakdown.
            Assert.Equivalent(b, pricer.Price(draft, menu).Breakdown, strict: true);
        }

        Assert.True(priced > Orders * 0.8, $"too few valid orders generated ({priced})");
    }

    [Fact]
    public void Line_order_does_not_change_the_price()
    {
        var lines = new[] { Line("large", 2, "bacon"), Line("small", 1, "onions", "garlic"), Line("medium", 3) };
        var forward = Pricer().Price(Draft(lines, "ILOVEPIZZA", Percent("18")), menu).Priced();
        var reversed = Pricer().Price(Draft(lines.Reverse(), "ILOVEPIZZA", Percent("18")), menu).Priced();
        Assert.Equal(forward.Total, reversed.Total);
        Assert.Equal(forward.Tax, reversed.Tax);
    }

    [Fact]
    public void SAVE5_boundary_on_real_menu_items()
    {
        // 4 × Small = 20.00: not over $20. 3 × Small + 1 × Small with pepperoni = 20.40: over $20.
        Assert.False(Pricer().Price(Draft([Line("small", 4)], "SAVE5"), menu).IsValid);
        var b = Pricer().Price(Draft([Line("small", 3), Line("small", 1, "pepperoni")], "SAVE5"), menu).Priced();
        Assert.Equal(20.40m, b.Subtotal);
        Assert.Equal(5.00m, b.Discount);
    }

    private DraftLineInput RandomLine(Random random)
    {
        var size = menu.Sizes[random.Next(menu.Sizes.Count)].Code;
        var toppings = menu.Toppings.OrderBy(_ => random.Next()).Take(random.Next(0, 6)).Select(t => t.Code).ToArray();
        return Line(size, random.Next(1, 21), toppings);
    }

    private OrderDraft RandomDraft(Random random)
    {
        var lines = Enumerable.Range(0, random.Next(1, 8)).Select(_ => RandomLine(random)).ToList();
        string? promo = random.Next(3) switch { 0 => null, 1 => "ILOVEPIZZA", _ => "SAVE5" };
        TipInput? tip = random.Next(3) switch
        {
            0 => null,
            1 => Amount($"{random.Next(0, 5000) / 100m:0.00}"),
            _ => Percent($"{random.Next(0, 3001) / 100m:0.##}"),
        };
        return Draft(lines, promo, tip);
    }

    private static IEnumerable<decimal> Amounts(PriceBreakdown b) =>
        new[] { b.Subtotal, b.Discount, b.Tax, b.Tip, b.Total }
            .Concat(b.Lines.SelectMany(l => new[] { l.BasePrice, l.UnitPrice, l.LineTotal }.Concat(l.Toppings.Select(t => t.Price))));
}
