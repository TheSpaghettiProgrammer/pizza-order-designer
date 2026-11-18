using PizzaOrderDesigner.Api.Pricing;
using static PizzaOrderDesigner.Api.Tests.Pricing.PricingTestSupport;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

public class OrderPricerTests
{
    private readonly MenuSnapshot menu = TestMenu.FromSeed();

    [Fact]
    public void Worked_example_from_the_API_contract()
    {
        // 2 × Large with pepperoni (0.96) and bacon (1.32): unit 14.28, line 28.56.
        // ILOVEPIZZA: 2.856 → 2.86. Taxable 25.70. Tax 1.6705 → 1.67. Tip 15% of 25.70 = 3.855 → 3.86.
        var b = Pricer().Price(Draft([Line("large", 2, "pepperoni", "bacon")], "ILOVEPIZZA", Percent("15")), menu).Priced();

        var line = Assert.Single(b.Lines);
        Assert.Equal(14.28m, line.UnitPrice);
        Assert.Equal(28.56m, line.LineTotal);
        Assert.Equal([0.96m, 1.32m], line.Toppings.Select(t => t.Price));
        Assert.Equal(28.56m, b.Subtotal);
        Assert.Equal("ILOVEPIZZA", b.PromoCode);
        Assert.Equal(2.86m, b.Discount);
        Assert.Equal(1.67m, b.Tax);
        Assert.Equal(3.86m, b.Tip);
        Assert.Equal(31.23m, b.Total);
    }

    [Fact]
    public void Plain_order_without_promo_or_tip()
    {
        var b = Pricer().Price(Draft([Line("small"), Line("medium", 3, "onions")]), menu).Priced();

        Assert.Equal(5.00m + (7.00m + 0.56m) * 3, b.Subtotal); // 27.68
        Assert.Equal(0m, b.Discount);
        Assert.Equal(1.80m, b.Tax);                            // 1.7992 → 1.80
        Assert.Equal(0m, b.Tip);
        Assert.Equal(29.48m, b.Total);
    }

    [Fact]
    public void Tax_is_charged_on_the_subtotal_after_the_discount()
    {
        // 4 × Large (48.00) with SAVE5 → taxable 43.00 → tax 2.795 → 2.80 (not 3.12 on 48.00)
        var b = Pricer().Price(Draft([Line("large", 4)], "SAVE5"), menu).Priced();
        Assert.Equal(5.00m, b.Discount);
        Assert.Equal(2.80m, b.Tax);
        Assert.Equal(45.80m, b.Total);
    }

    [Fact]
    public void Percent_tip_is_on_the_discounted_pre_tax_amount_with_SAVE5()
    {
        // 4 × Large = 48.00, SAVE5 → 43.00; tip 20% of 43.00 = 8.60; tax 2.795 → 2.80
        var b = Pricer().Price(Draft([Line("large", 4)], "SAVE5", Percent("20")), menu).Priced();
        Assert.Equal(8.60m, b.Tip);
        Assert.Equal(54.40m, b.Total);
    }

    [Fact]
    public void A_null_line_is_a_field_error_not_a_crash()
    {
        var r = Pricer().Price(new OrderDraft { Lines = [Line("small"), null!] }, menu);
        Assert.Equal(["This item is empty."], r.Errors["lines[1]"]);
    }

    [Fact]
    public void Tip_as_a_dollar_amount_is_added_untaxed()
    {
        var b = Pricer().Price(Draft([Line("large")], tip: Amount("2.50")), menu).Priced();
        Assert.Equal(0.78m, b.Tax); // 12 × 6.5%, the tip is not in the taxable amount
        Assert.Equal(2.50m, b.Tip);
        Assert.Equal(15.28m, b.Total);
    }

    [Theory]
    [InlineData(TipBase.PreTax, "2.40")]  // 20% of 12.00
    [InlineData(TipBase.PostTax, "2.56")] // 20% of 12.78 = 2.556 → 2.56
    public void Tip_percent_base_is_configurable(TipBase tipBase, string expectedTip)
    {
        var b = Pricer(tipBase).Price(Draft([Line("large")], tip: Percent("20")), menu).Priced();
        Assert.Equal(decimal.Parse(expectedTip), b.Tip);
        Assert.Equal(tipBase.ToString(), b.TipBase);
    }

    [Fact]
    public void Description_lists_quantity_size_and_toppings_for_each_line()
    {
        var b = Pricer().Price(Draft([Line("large", 2, "pepperoni", "bacon"), Line("small")]), menu).Priced();
        Assert.Equal("2 × Large pizza — Pepperoni, Bacon", b.Lines[0].Description);
        Assert.Equal("1 × Small pizza — no toppings", b.Lines[1].Description);
        Assert.Equal("2 × Large pizza — Pepperoni, Bacon\n1 × Small pizza — no toppings", b.Description);
    }

    [Fact]
    public void Codes_are_case_insensitive_and_the_response_uses_menu_codes()
    {
        var b = Pricer().Price(Draft([Line("LARGE", 1, "Pepperoni")], "save5 "), menu);
        Assert.False(b.IsValid); // 12.96 is not over 20 → SAVE5 rejected, proving the code was recognised
        Assert.Equal(["SAVE5 needs an order over $20.00 before tax."], b.Errors["promoCode"]);

        var ok = Pricer().Price(Draft([Line("LARGE", 1, "Pepperoni")]), menu).Priced();
        Assert.Equal("large", ok.Lines[0].SizeCode);
        Assert.Equal("pepperoni", ok.Lines[0].Toppings[0].Code);
    }

    // ---- Validation (D-043)

    [Fact]
    public void An_empty_order_is_rejected()
    {
        var r = Pricer().Price(Draft([]), menu);
        Assert.Equal(["Your order needs at least one pizza."], r.Errors["lines"]);
        Assert.Equal(["Your order needs at least one pizza."], Pricer().Price(new OrderDraft(), menu).Errors["lines"]);
    }

    [Fact]
    public void More_than_25_lines_is_rejected()
    {
        var r = Pricer().Price(Draft(Enumerable.Range(0, 26).Select(_ => Line("small"))), menu);
        Assert.Equal(["An order can have at most 25 items."], r.Errors["lines"]);
        Assert.True(Pricer().Price(Draft(Enumerable.Range(0, 25).Select(_ => Line("small"))), menu).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Quantity_outside_1_to_20_is_rejected(int quantity)
    {
        var r = Pricer().Price(Draft([Line("small", quantity)]), menu);
        Assert.Equal(["Quantity must be between 1 and 20."], r.Errors["lines[0].quantity"]);
    }

    [Fact]
    public void Unknown_size_and_topping_and_duplicate_topping_are_field_errors()
    {
        var r = Pricer().Price(Draft([Line("huge", 1, "anchovies"), Line("small", 1, "bacon", "BACON")]), menu);
        Assert.Equal(["'huge' isn't a size on our menu."], r.Errors["lines[0].sizeCode"]);
        Assert.Equal(["'anchovies' isn't a topping on our menu."], r.Errors["lines[0].toppingCodes"]);
        Assert.Equal(["Bacon is already on this pizza."], r.Errors["lines[1].toppingCodes"]);
    }

    [Theory]
    [InlineData("amount", "-1", "Enter the tip as an amount, like 2.50.")]
    [InlineData("amount", "abc", "Enter the tip as an amount, like 2.50.")]
    [InlineData("amount", "2.505", "A tip can have at most 2 decimal places.")]
    [InlineData("amount", "500.01", "A tip can't be more than $500.00.")]
    [InlineData("percent", "100.5", "A tip percentage must be between 0 and 100.")]
    [InlineData("percent", "1e2", "Enter the tip as a percentage, like 15.")]
    [InlineData("cash", "5", "Choose a tip as an amount or a percentage.")]
    public void Invalid_tips_get_clear_messages(string mode, string value, string message)
    {
        var r = Pricer().Price(Draft([Line("small")], tip: new TipInput { Mode = mode, Value = value }), menu);
        Assert.False(r.IsValid);
        Assert.Contains(message, r.Errors.Values.SelectMany(v => v));
    }

    [Theory]
    [InlineData("amount", "0")]
    [InlineData("amount", "500.00")]
    [InlineData("percent", "0")]
    [InlineData("percent", "100")]
    [InlineData("percent", " 17.5 ")]
    public void Boundary_tips_are_accepted(string mode, string value)
    {
        Assert.True(Pricer().Price(Draft([Line("small")], tip: new TipInput { Mode = mode, Value = value }), menu).IsValid);
    }

    [Fact]
    public void All_problems_are_reported_together()
    {
        var r = Pricer().Price(Draft([Line("small", 0)], tip: Amount("x")), menu);
        Assert.Contains("lines[0].quantity", r.Errors.Keys);
        Assert.Contains("tip.value", r.Errors.Keys);
    }
}
