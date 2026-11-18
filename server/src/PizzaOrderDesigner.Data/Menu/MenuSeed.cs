namespace PizzaOrderDesigner.Data.Menu;

/// <summary>
/// The restaurant's menu as given in the brief. Single source for the EF seed (migrations) and the unit tests,
/// so tests price against exactly the data the database holds.
/// </summary>
public static class MenuSeed
{
    public const string PercentOffOrder = "PercentOffOrder";
    public const string AmountOffOverThreshold = "AmountOffOverThreshold";

    public static IReadOnlyList<Size> Sizes { get; } =
    [
        new() { Id = 1, Code = "small", Name = "Small", BasePrice = 5.00m, SortOrder = 1 },
        new() { Id = 2, Code = "medium", Name = "Medium", BasePrice = 7.00m, SortOrder = 2 },
        new() { Id = 3, Code = "large", Name = "Large", BasePrice = 12.00m, SortOrder = 3 },
    ];

    public static IReadOnlyList<ToppingCategory> Categories { get; } =
    [
        new() { Id = 1, Code = "basic", Name = "Basic", Rate = 0.08m, SortOrder = 1 },
        new() { Id = 2, Code = "premium", Name = "Premium", Rate = 0.11m, SortOrder = 2 },
    ];

    public static IReadOnlyList<Topping> Toppings { get; } =
    [
        new() { Id = 1, Code = "pepperoni", Name = "Pepperoni", CategoryId = 1, SortOrder = 1 },
        new() { Id = 2, Code = "sausage", Name = "Sausage", CategoryId = 1, SortOrder = 2 },
        new() { Id = 3, Code = "extra-cheese", Name = "Extra cheese", CategoryId = 1, SortOrder = 3 },
        new() { Id = 4, Code = "onions", Name = "Onions", CategoryId = 1, SortOrder = 4 },
        new() { Id = 5, Code = "black-olives", Name = "Black olives", CategoryId = 1, SortOrder = 5 },
        new() { Id = 6, Code = "green-peppers", Name = "Green peppers", CategoryId = 1, SortOrder = 6 },
        new() { Id = 7, Code = "bacon", Name = "Bacon", CategoryId = 2, SortOrder = 7 },
        new() { Id = 8, Code = "chicken", Name = "Chicken", CategoryId = 2, SortOrder = 8 },
        new() { Id = 9, Code = "garlic", Name = "Garlic", CategoryId = 2, SortOrder = 9 },
        new() { Id = 10, Code = "artichoke-hearts", Name = "Artichoke hearts", CategoryId = 2, SortOrder = 10 },
        new() { Id = 11, Code = "sun-dried-tomatoes", Name = "Sun-dried tomatoes", CategoryId = 2, SortOrder = 11 },
        new() { Id = 12, Code = "pineapple", Name = "Pineapple", CategoryId = 2, SortOrder = 12 },
    ];

    public static IReadOnlyList<Promotion> Promotions { get; } =
    [
        new() { Id = 1, Code = "ILOVEPIZZA", RuleType = PercentOffOrder, Percent = 0.10m, ExclusionGroup = "order-discount" },
        new() { Id = 2, Code = "SAVE5", RuleType = AmountOffOverThreshold, Amount = 5.00m, MinSubtotalExclusive = 20.00m, ExclusionGroup = "order-discount" },
    ];
}
