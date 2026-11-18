namespace PizzaOrderDesigner.Api.Pricing.Promotions;

public sealed record AppliedPromotion(string Code, decimal Discount);

/// <summary>Either the applied promotions and the total discount, or a customer-facing reason they can't apply.</summary>
public sealed record PromotionResult(IReadOnlyList<AppliedPromotion> Applied, decimal Discount, string? Error)
{
    public static readonly PromotionResult None = new([], 0m, null);

    public bool IsValid => Error is null;
}

/// <summary>
/// Applies promo codes to a subtotal. The API takes one code per order (D-043), but the engine handles a list so
/// that conflicts are modelled explicitly: promos sharing an exclusion group can't be combined (SAVE5 ⟂ ILOVEPIZZA).
/// </summary>
public sealed class PromotionEngine
{
    private readonly Dictionary<string, IPromotionRule> rulesByType;

    public PromotionEngine(IEnumerable<IPromotionRule> rules)
    {
        rulesByType = rules.ToDictionary(r => r.RuleType, StringComparer.Ordinal);
    }

    public PromotionResult Apply(IReadOnlyList<string> codes, decimal subtotal, MenuSnapshot menu)
    {
        var normalized = codes
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0)
        {
            return PromotionResult.None;
        }

        var promotions = new List<PromotionInfo>();
        foreach (var code in normalized)
        {
            var promotion = menu.FindPromotion(code);
            if (promotion is null)
            {
                return Invalid($"Promo code '{code.ToUpperInvariant()}' isn't valid.");
            }

            promotions.Add(promotion);
        }

        var conflict = promotions
            .Where(p => p.ExclusionGroup is not null)
            .GroupBy(p => p.ExclusionGroup, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (conflict is not null)
        {
            var names = conflict.Select(p => p.Code).ToList();
            return Invalid($"{names[0]} can't be combined with {string.Join(" or ", names.Skip(1))}.");
        }

        var context = new PromotionContext(subtotal);
        var applied = new List<AppliedPromotion>();
        foreach (var promotion in promotions)
        {
            if (!rulesByType.TryGetValue(promotion.RuleType, out var rule))
            {
                throw new InvalidOperationException($"Promotion {promotion.Code} uses unknown rule type '{promotion.RuleType}'.");
            }

            var outcome = rule.Evaluate(promotion, context);
            if (!outcome.Eligible)
            {
                return Invalid(outcome.IneligibleReason!);
            }

            applied.Add(new AppliedPromotion(promotion.Code, outcome.Discount));
        }

        // A discount can never exceed what is being discounted (the total is never negative).
        var discount = Math.Min(subtotal, applied.Sum(a => a.Discount));
        return new PromotionResult(applied, discount, null);
    }

    private static PromotionResult Invalid(string reason) => new([], 0m, reason);
}
