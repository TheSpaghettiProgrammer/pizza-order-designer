using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

public class MenuSnapshotTests
{
    private readonly MenuSnapshot menu = TestMenu.FromSeed();

    [Theory]
    [InlineData("small", "pepperoni", "0.40")]   // 5 × 8%
    [InlineData("medium", "pepperoni", "0.56")]  // 7 × 8%
    [InlineData("large", "pepperoni", "0.96")]   // 12 × 8%
    [InlineData("small", "bacon", "0.55")]       // 5 × 11%
    [InlineData("medium", "bacon", "0.77")]      // 7 × 11%
    [InlineData("large", "bacon", "1.32")]       // 12 × 11%
    public void Topping_price_is_category_rate_times_size_base(string size, string topping, string expected)
    {
        var price = menu.ToppingPrice(menu.FindSize(size)!, menu.FindTopping(topping)!);
        Assert.Equal(decimal.Parse(expected), price);
    }

    [Fact]
    public void Seed_matches_the_brief()
    {
        Assert.Equal(["small", "medium", "large"], menu.Sizes.Select(s => s.Code));
        Assert.Equal(12, menu.Toppings.Count);
        Assert.Equal(6, menu.Toppings.Count(t => t.CategoryCode == "basic"));
        Assert.Equal(6, menu.Toppings.Count(t => t.CategoryCode == "premium"));
        Assert.Equal(["ILOVEPIZZA", "SAVE5"], menu.Promotions.Select(p => p.Code).Order());
    }

    [Fact]
    public void Lookups_are_case_insensitive_and_unknown_codes_return_null()
    {
        Assert.NotNull(menu.FindSize("LARGE"));
        Assert.NotNull(menu.FindTopping("Pepperoni"));
        Assert.NotNull(menu.FindPromotion("ilovepizza"));
        Assert.Null(menu.FindSize("xxl"));
        Assert.Null(menu.FindTopping(null));
    }

    [Fact]
    public void A_new_size_and_category_are_priced_from_data_alone()
    {
        // Extensibility (#8, Requirement 6): no code change, only data.
        var extended = new MenuSnapshot(
            [.. menu.Sizes, new SizeInfo("xl", "Extra large", 15.00m, 4)],
            [.. menu.Categories, new ToppingCategoryInfo("gourmet", "Gourmet", 0.15m, 3)],
            [.. menu.Toppings, new ToppingInfo("truffle", "Truffle", "gourmet", 13)],
            menu.Promotions);

        Assert.Equal(2.25m, extended.ToppingPrice(extended.FindSize("xl")!, extended.FindTopping("truffle")!));
        Assert.Equal(1.20m, extended.ToppingPrice(extended.FindSize("xl")!, extended.FindTopping("pepperoni")!));
    }

    [Fact]
    public void A_topping_in_an_unknown_category_is_rejected_at_load()
    {
        Assert.Throws<InvalidOperationException>(() => new MenuSnapshot(
            menu.Sizes, menu.Categories, [new ToppingInfo("x", "X", "nope", 1)], []));
    }
}
