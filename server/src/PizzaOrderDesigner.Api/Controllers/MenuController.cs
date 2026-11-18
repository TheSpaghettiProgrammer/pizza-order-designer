using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Controllers;

[ApiController]
[Route("api/menu")]
public sealed class MenuController(IMenuProvider menuProvider, IOptions<PricingOptions> pricingOptions) : ControllerBase
{
    /// <summary>Everything the order builder needs to render choices, with topping prices precomputed per size.</summary>
    [HttpGet]
    public async Task<MenuResponse> Get(CancellationToken cancellationToken)
    {
        var menu = await menuProvider.GetMenuAsync(cancellationToken);
        var options = pricingOptions.Value;
        return new MenuResponse(
            menu.Sizes.Select(s => new MenuSizeDto(s.Code, s.Name, s.BasePrice)).ToList(),
            menu.Categories.Select(c => new MenuCategoryDto(
                c.Code,
                c.Name,
                c.Rate,
                menu.Toppings
                    .Where(t => string.Equals(t.CategoryCode, c.Code, StringComparison.OrdinalIgnoreCase))
                    .Select(t => new MenuToppingDto(
                        t.Code,
                        t.Name,
                        menu.Sizes.ToDictionary(s => s.Code, s => menu.ToppingPrice(s, t))))
                    .ToList()))
                .ToList(),
            options.TaxRate,
            options.TipBase.ToString());
    }
}

public sealed record MenuResponse(
    IReadOnlyList<MenuSizeDto> Sizes,
    IReadOnlyList<MenuCategoryDto> ToppingCategories,
    decimal TaxRate,
    string TipBase);

public sealed record MenuSizeDto(string Code, string Name, decimal BasePrice);

public sealed record MenuCategoryDto(string Code, string Name, decimal Rate, IReadOnlyList<MenuToppingDto> Toppings);

public sealed record MenuToppingDto(string Code, string Name, IReadOnlyDictionary<string, decimal> PricesBySize);
