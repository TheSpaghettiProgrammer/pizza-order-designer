namespace PizzaOrderDesigner.Api.Pricing;

/// <summary>Written description of an order (Requirement 3), built from menu display names.</summary>
public static class OrderDescription
{
    public static string Line(int quantity, string sizeName, IEnumerable<string> toppingNames)
    {
        var toppings = toppingNames.ToList();
        var toppingText = toppings.Count == 0 ? "no toppings" : string.Join(", ", toppings);
        return $"{quantity} × {sizeName} pizza — {toppingText}";
    }

    public static string Order(IEnumerable<string> lineDescriptions) => string.Join("\n", lineDescriptions);
}
