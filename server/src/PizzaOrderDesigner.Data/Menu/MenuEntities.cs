namespace PizzaOrderDesigner.Data.Menu;

/// <summary>A pizza size. Its base price drives every topping price (category rate × base).</summary>
public class Size
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal BasePrice { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A topping category; a topping costs <see cref="Rate"/> × the size's base price.</summary>
public class ToppingCategory
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
    public List<Topping> Toppings { get; set; } = [];
}

public class Topping
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public ToppingCategory? Category { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// A promo code. <see cref="RuleType"/> picks the rule class that evaluates it; the remaining columns are that
/// rule's parameters. Promos sharing an <see cref="ExclusionGroup"/> cannot be combined.
/// </summary>
public class Promotion
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string RuleType { get; set; }
    public decimal? Percent { get; set; }
    public decimal? Amount { get; set; }
    public decimal? MinSubtotalExclusive { get; set; }
    public string? ExclusionGroup { get; set; }
    public bool IsActive { get; set; } = true;
}
