using PizzaOrderDesigner.Data.Menu;

namespace PizzaOrderDesigner.Api.Pricing;

public sealed record SizeInfo(string Code, string Name, decimal BasePrice, int SortOrder);

public sealed record ToppingCategoryInfo(string Code, string Name, decimal Rate, int SortOrder);

public sealed record ToppingInfo(string Code, string Name, string CategoryCode, int SortOrder);

public sealed record PromotionInfo(
    string Code,
    string RuleType,
    decimal? Percent = null,
    decimal? Amount = null,
    decimal? MinSubtotalExclusive = null,
    string? ExclusionGroup = null);

/// <summary>
/// An immutable, in-memory view of the menu that pricing runs against. Pricing never queries the database
/// and never contains menu constants: adding a size, topping, category or promo is a data change (#8, D-038).
/// Lookups by code are case-insensitive.
/// </summary>
public sealed class MenuSnapshot
{
    public MenuSnapshot(
        IEnumerable<SizeInfo> sizes,
        IEnumerable<ToppingCategoryInfo> categories,
        IEnumerable<ToppingInfo> toppings,
        IEnumerable<PromotionInfo> promotions)
    {
        Sizes = sizes.OrderBy(s => s.SortOrder).ToList();
        Categories = categories.OrderBy(c => c.SortOrder).ToList();
        Toppings = toppings.OrderBy(t => t.SortOrder).ToList();
        Promotions = promotions.ToList();
        sizesByCode = Sizes.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        categoriesByCode = Categories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        toppingsByCode = Toppings.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        promotionsByCode = Promotions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        var orphan = Toppings.FirstOrDefault(t => !categoriesByCode.ContainsKey(t.CategoryCode));
        if (orphan is not null)
        {
            throw new InvalidOperationException($"Topping '{orphan.Code}' refers to unknown category '{orphan.CategoryCode}'.");
        }
    }

    private readonly Dictionary<string, SizeInfo> sizesByCode;
    private readonly Dictionary<string, ToppingCategoryInfo> categoriesByCode;
    private readonly Dictionary<string, ToppingInfo> toppingsByCode;
    private readonly Dictionary<string, PromotionInfo> promotionsByCode;

    public IReadOnlyList<SizeInfo> Sizes { get; }
    public IReadOnlyList<ToppingCategoryInfo> Categories { get; }
    public IReadOnlyList<ToppingInfo> Toppings { get; }
    public IReadOnlyList<PromotionInfo> Promotions { get; }

    public SizeInfo? FindSize(string? code) => code is not null && sizesByCode.TryGetValue(code, out var s) ? s : null;

    public ToppingInfo? FindTopping(string? code) => code is not null && toppingsByCode.TryGetValue(code, out var t) ? t : null;

    public PromotionInfo? FindPromotion(string? code) => code is not null && promotionsByCode.TryGetValue(code, out var p) ? p : null;

    public ToppingCategoryInfo CategoryOf(ToppingInfo topping) => categoriesByCode[topping.CategoryCode];

    /// <summary>A topping's price on a given size: category rate × size base price, rounded once to cents.</summary>
    public decimal ToppingPrice(SizeInfo size, ToppingInfo topping) =>
        Money.RoundToCents(size.BasePrice * CategoryOf(topping).Rate);

    /// <summary>Builds a snapshot from the EF entities (active promotions only).</summary>
    public static MenuSnapshot From(
        IEnumerable<Size> sizes,
        IEnumerable<ToppingCategory> categories,
        IEnumerable<Topping> toppings,
        IEnumerable<Promotion> promotions)
    {
        var categoryList = categories.ToList();
        var categoryCodeById = categoryList.ToDictionary(c => c.Id, c => c.Code);
        return new MenuSnapshot(
            sizes.Select(s => new SizeInfo(s.Code, s.Name, s.BasePrice, s.SortOrder)),
            categoryList.Select(c => new ToppingCategoryInfo(c.Code, c.Name, c.Rate, c.SortOrder)),
            toppings.Select(t => new ToppingInfo(t.Code, t.Name, categoryCodeById[t.CategoryId], t.SortOrder)),
            promotions.Where(p => p.IsActive).Select(p =>
                new PromotionInfo(p.Code, p.RuleType, p.Percent, p.Amount, p.MinSubtotalExclusive, p.ExclusionGroup)));
    }
}
