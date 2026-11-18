namespace PizzaOrderDesigner.Api.Pricing;

/// <summary>What a percentage tip is calculated on (D-038).</summary>
public enum TipBase
{
    /// <summary>(subtotal − discount): before tax. The default, as in Toast's default setting.</summary>
    PreTax,

    /// <summary>(subtotal − discount + tax): the literal "total bill".</summary>
    PostTax,
}

/// <summary>Pricing settings, bound from the "Pricing" configuration section.</summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public decimal TaxRate { get; set; } = 0.065m;
    public TipBase TipBase { get; set; } = TipBase.PreTax;

    // Validation limits (D-043).
    public int MaxLines { get; set; } = 25;
    public int MaxQuantityPerLine { get; set; } = 20;
    public decimal MaxTipAmount { get; set; } = 500.00m;
    public decimal MaxTipPercent { get; set; } = 100m;
}
