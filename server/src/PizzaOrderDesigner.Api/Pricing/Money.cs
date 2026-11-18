namespace PizzaOrderDesigner.Api.Pricing;

/// <summary>
/// The one rounding rule for money (D-038). All prices are <see cref="decimal"/>; nothing is ever a float.
/// </summary>
public static class Money
{
    /// <summary>
    /// Rounds to whole cents, half away from zero (0.005 → 0.01). Explicit, because .NET's default
    /// <see cref="Math.Round(decimal, int)"/> is banker's rounding (2.5 → 2), which is not how sales tax rounds.
    /// </summary>
    public static decimal RoundToCents(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>True when the amount has no more than two decimal places.</summary>
    public static bool HasAtMostTwoDecimals(decimal amount) => amount == Math.Round(amount, 2);
}
