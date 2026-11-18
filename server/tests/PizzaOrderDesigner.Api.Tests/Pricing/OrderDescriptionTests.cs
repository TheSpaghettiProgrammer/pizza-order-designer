using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

public class OrderDescriptionTests
{
    [Fact]
    public void Line_lists_quantity_size_and_toppings()
    {
        Assert.Equal("3 × Medium pizza — Onions, Garlic", OrderDescription.Line(3, "Medium", ["Onions", "Garlic"]));
        Assert.Equal("1 × Small pizza — no toppings", OrderDescription.Line(1, "Small", []));
    }

    [Fact]
    public void Order_puts_one_line_per_item()
    {
        Assert.Equal("a\nb", OrderDescription.Order(["a", "b"]));
    }
}
