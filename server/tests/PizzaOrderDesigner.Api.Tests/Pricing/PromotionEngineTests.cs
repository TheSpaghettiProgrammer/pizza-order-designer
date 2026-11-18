using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Api.Pricing.Promotions;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

public class PromotionEngineTests
{
    private readonly MenuSnapshot menu = TestMenu.FromSeed();
    private readonly PromotionEngine engine = new([new PercentOffOrderRule(), new AmountOffOverThresholdRule()]);

    [Fact]
    public void ILOVEPIZZA_takes_10_percent_rounded_half_up()
    {
        var result = engine.Apply(["ILOVEPIZZA"], 28.56m, menu);
        Assert.True(result.IsValid);
        Assert.Equal(2.86m, result.Discount); // 2.856 → 2.86
    }

    [Theory]
    [InlineData("20.00", false)] // "over $20" is strict: exactly $20.00 does not qualify
    [InlineData("20.01", true)]
    [InlineData("19.99", false)]
    [InlineData("40.00", true)]
    public void SAVE5_requires_a_subtotal_strictly_over_20(string subtotal, bool eligible)
    {
        var result = engine.Apply(["SAVE5"], decimal.Parse(subtotal), menu);
        Assert.Equal(eligible, result.IsValid);
        Assert.Equal(eligible ? 5.00m : 0m, result.Discount);
        if (!eligible)
        {
            Assert.Equal("SAVE5 needs an order over $20.00 before tax.", result.Error);
        }
    }

    [Fact]
    public void SAVE5_and_ILOVEPIZZA_cannot_be_combined()
    {
        var result = engine.Apply(["SAVE5", "ILOVEPIZZA"], 50m, menu);
        Assert.False(result.IsValid);
        Assert.Equal("SAVE5 can't be combined with ILOVEPIZZA.", result.Error);
        Assert.Equal(0m, result.Discount);
    }

    [Theory]
    [InlineData("  ilovepizza ")]
    [InlineData("ILovePizza")]
    public void Codes_are_trimmed_and_case_insensitive(string code)
    {
        Assert.Equal(1.00m, engine.Apply([code], 10.00m, menu).Discount);
    }

    [Fact]
    public void Unknown_code_is_rejected_with_a_clear_message()
    {
        var result = engine.Apply(["FREEPIZZA"], 30m, menu);
        Assert.Equal("Promo code 'FREEPIZZA' isn't valid.", result.Error);
    }

    [Fact]
    public void No_code_means_no_discount()
    {
        Assert.Equal(PromotionResult.None, engine.Apply([], 30m, menu));
        Assert.Equal(PromotionResult.None, engine.Apply(["   "], 30m, menu));
    }

    [Fact]
    public void A_new_promo_of_an_existing_type_is_data_only_and_conflicts_come_from_data()
    {
        // Extensibility: SAVE10 ($10 off over $50) is a new row, not new code. Sharing the exclusion group makes it
        // conflict with SAVE5; a promo without a group combines freely.
        var extended = new MenuSnapshot(menu.Sizes, menu.Categories, menu.Toppings,
        [
            .. menu.Promotions,
            new PromotionInfo("SAVE10", "AmountOffOverThreshold", Amount: 10m, MinSubtotalExclusive: 50m, ExclusionGroup: "order-discount"),
            new PromotionInfo("TUESDAY", "PercentOffOrder", Percent: 0.05m),
        ]);

        Assert.Equal(10m, engine.Apply(["SAVE10"], 60m, extended).Discount);
        Assert.Equal("SAVE10 needs an order over $50.00 before tax.", engine.Apply(["SAVE10"], 50m, extended).Error);
        Assert.False(engine.Apply(["SAVE5", "SAVE10"], 60m, extended).IsValid);
        Assert.Equal(8.00m, engine.Apply(["SAVE5", "TUESDAY"], 60m, extended).Discount); // 5 + 3
    }

    [Fact]
    public void Discount_never_exceeds_the_subtotal()
    {
        var generous = new MenuSnapshot(menu.Sizes, menu.Categories, menu.Toppings,
            [new PromotionInfo("BIG", "AmountOffOverThreshold", Amount: 100m, MinSubtotalExclusive: 0m)]);
        Assert.Equal(25.00m, engine.Apply(["BIG"], 25.00m, generous).Discount);
    }
}
