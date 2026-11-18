using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Data;

namespace PizzaOrderDesigner.Api.Menu;

/// <summary>Supplies the current menu to pricing and to the menu endpoint.</summary>
public interface IMenuProvider
{
    Task<MenuSnapshot> GetMenuAsync(CancellationToken cancellationToken);
}

/// <summary>Reads the menu from Postgres on each call (the menu is tiny; no cache needed for this exercise).</summary>
public sealed class EfMenuProvider(PizzaDbContext db) : IMenuProvider
{
    public async Task<MenuSnapshot> GetMenuAsync(CancellationToken cancellationToken)
    {
        var sizes = await db.Sizes.AsNoTracking().ToListAsync(cancellationToken);
        var categories = await db.ToppingCategories.AsNoTracking().ToListAsync(cancellationToken);
        var toppings = await db.Toppings.AsNoTracking().ToListAsync(cancellationToken);
        var promotions = await db.Promotions.AsNoTracking().ToListAsync(cancellationToken);
        return MenuSnapshot.From(sizes, categories, toppings, promotions);
    }
}
