using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Data.Menu;

namespace PizzaOrderDesigner.Api.Tests;

/// <summary>Builds menu snapshots for tests from the same seed data the database migration uses.</summary>
public static class TestMenu
{
    public static MenuSnapshot FromSeed() =>
        MenuSnapshot.From(MenuSeed.Sizes, MenuSeed.Categories, MenuSeed.Toppings, MenuSeed.Promotions);
}
