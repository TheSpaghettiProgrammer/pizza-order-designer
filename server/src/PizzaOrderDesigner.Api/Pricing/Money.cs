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
    /// <remarks>
    /// The result always carries exactly two decimal places (scale 2), so it serializes as <c>5.00</c>, never <c>5</c>
    /// or <c>2.5</c>. <see cref="Math.Round(decimal, int, MidpointRounding)"/> never raises the scale, so adding 0.00m does.
    /// </remarks>
    public static decimal RoundToCents(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero) + 0.00m;

    /// <summary>Formats an amount as dollars for customer messages, independent of the server's culture.</summary>
    public static string Format(decimal amount) =>
        "$" + RoundToCents(amount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True when the amount has no more than two decimal places.</summary>
    public static bool HasAtMostTwoDecimals(decimal amount) => amount == Math.Round(amount, 2);
}
