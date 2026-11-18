using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Tests.Pricing;

public class MoneyTests
{
    [Theory]
    [InlineData("0.005", "0.01")]   // midpoint rounds up, not to even
    [InlineData("0.015", "0.02")]
    [InlineData("2.675", "2.68")]   // decimal is exact, so no binary-float surprise
    [InlineData("3.855", "3.86")]
    [InlineData("1.6705", "1.67")]
    [InlineData("0.004", "0.00")]
    [InlineData("12.00", "12.00")]
    public void RoundToCents_rounds_half_away_from_zero(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected), Money.RoundToCents(decimal.Parse(input)));
    }

    [Fact]
    public void Default_Math_Round_would_be_wrong_for_money()
    {
        // Documents why Money.RoundToCents exists: banker's rounding sends 0.125 to 0.12.
        Assert.Equal(0.12m, Math.Round(0.125m, 2));
        Assert.Equal(0.13m, Money.RoundToCents(0.125m));
    }

    [Theory]
    [InlineData("2.5", true)]
    [InlineData("2.50", true)]
    [InlineData("2.505", false)]
    public void HasAtMostTwoDecimals(string input, bool expected)
    {
        Assert.Equal(expected, Money.HasAtMostTwoDecimals(decimal.Parse(input)));
    }
}
