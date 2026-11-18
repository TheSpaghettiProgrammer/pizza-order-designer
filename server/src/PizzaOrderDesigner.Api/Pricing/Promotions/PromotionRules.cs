using PizzaOrderDesigner.Data.Menu;

namespace PizzaOrderDesigner.Api.Pricing.Promotions;

/// <summary>What a promotion rule sees: amounts before discount, tax and tip.</summary>
public sealed record PromotionContext(decimal Subtotal);

public sealed record PromotionOutcome(bool Eligible, decimal Discount, string? IneligibleReason)
{
    public static PromotionOutcome Applies(decimal discount) => new(true, discount, null);

    public static PromotionOutcome NotEligible(string reason) => new(false, 0m, reason);
}

/// <summary>
/// One kind of promotion. A promo code (data) names its <see cref="RuleType"/> and supplies parameters. A new promo
/// of an existing kind is data only; a new kind is one new class registered in DI. The pricer never changes.
/// </summary>
public interface IPromotionRule
{
    string RuleType { get; }

    PromotionOutcome Evaluate(PromotionInfo promotion, PromotionContext context);
}

/// <summary>"ILOVEPIZZA": <see cref="PromotionInfo.Percent"/> off the order total before tax.</summary>
public sealed class PercentOffOrderRule : IPromotionRule
{
    public string RuleType => MenuSeed.PercentOffOrder;

    public PromotionOutcome Evaluate(PromotionInfo promotion, PromotionContext context)
    {
        var percent = promotion.Percent
            ?? throw new InvalidOperationException($"Promotion {promotion.Code} ({RuleType}) has no Percent.");
        return PromotionOutcome.Applies(Money.RoundToCents(context.Subtotal * percent));
    }
}

/// <summary>"SAVE5": a fixed <see cref="PromotionInfo.Amount"/> off when the subtotal is strictly over the threshold.</summary>
public sealed class AmountOffOverThresholdRule : IPromotionRule
{
    public string RuleType => MenuSeed.AmountOffOverThreshold;

    public PromotionOutcome Evaluate(PromotionInfo promotion, PromotionContext context)
    {
        var amount = promotion.Amount
            ?? throw new InvalidOperationException($"Promotion {promotion.Code} ({RuleType}) has no Amount.");
        var threshold = promotion.MinSubtotalExclusive ?? 0m;
        return context.Subtotal > threshold
            ? PromotionOutcome.Applies(Money.RoundToCents(amount))
            : PromotionOutcome.NotEligible($"{promotion.Code} needs an order over {Money.Format(threshold)} before tax.");
    }
}
