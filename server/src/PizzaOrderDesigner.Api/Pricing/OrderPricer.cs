using System.Globalization;
using Microsoft.Extensions.Options;
using PizzaOrderDesigner.Api.Pricing.Promotions;

namespace PizzaOrderDesigner.Api.Pricing;

/// <summary>
/// The single source of truth for an order's price (D-039). Pure: no I/O, no clock. Quote, submit and update all
/// call this. Order of operations and rounding are D-038:
/// <code>
/// toppingPrice = round(sizeBase × categoryRate)      lineTotal = (sizeBase + Σ toppingPrice) × quantity
/// subtotal     = Σ lineTotal                         discount  = min(subtotal, promo(subtotal))
/// tax          = round((subtotal − discount) × rate) tip       = amount | round(tipBase × percent)
/// total        = subtotal − discount + tax + tip      (tip is never taxed)
/// </code>
/// </summary>
public sealed class OrderPricer(PromotionEngine promotions, IOptions<PricingOptions> options)
{
    private readonly PricingOptions settings = options.Value;

    public PricingResult Price(OrderDraft draft, MenuSnapshot menu)
    {
        var errors = new Dictionary<string, List<string>>();
        void Error(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                errors[field] = list = [];
            }

            list.Add(message);
        }

        // ---- Lines (#10)
        var lines = new List<PricedLine>();
        var inputLines = draft.Lines ?? [];
        if (inputLines.Count == 0)
        {
            Error("lines", "Your order needs at least one pizza.");
        }
        else if (inputLines.Count > settings.MaxLines)
        {
            Error("lines", $"An order can have at most {settings.MaxLines} items.");
        }
        else
        {
            for (var i = 0; i < inputLines.Count; i++)
            {
                var line = PriceLine(inputLines[i], $"lines[{i}]", menu, Error);
                if (line is not null)
                {
                    lines.Add(line);
                }
            }
        }

        // ---- Tip input (#12), validated even when lines are invalid so the customer sees every problem at once.
        var tip = ParseTip(draft.Tip, Error);

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        var subtotal = lines.Sum(l => l.LineTotal);

        // ---- Promotion (#11): one code per order on the API (D-043).
        var promoCodes = string.IsNullOrWhiteSpace(draft.PromoCode) ? Array.Empty<string>() : [draft.PromoCode];
        var promotion = promotions.Apply(promoCodes, subtotal, menu);
        if (!promotion.IsValid)
        {
            Error("promoCode", promotion.Error!);
            return Fail(errors);
        }

        // ---- Tax and tip (#12)
        var taxable = subtotal - promotion.Discount;
        var tax = Money.RoundToCents(taxable * settings.TaxRate);
        var tipAmount = tip switch
        {
            null => 0.00m, // scale 2, so it serializes as 0.00 like every other amount
            { IsPercent: false } => tip.Value,
            _ => Money.RoundToCents((settings.TipBase == TipBase.PostTax ? taxable + tax : taxable) * tip.Value / 100m),
        };
        var total = taxable + tax + tipAmount;

        var breakdown = new PriceBreakdown(
            lines,
            subtotal,
            promotion.Applied.Count > 0 ? string.Join(",", promotion.Applied.Select(a => a.Code)) : null,
            promotion.Discount,
            settings.TaxRate,
            tax,
            settings.TipBase.ToString(),
            tipAmount,
            total,
            OrderDescription.Order(lines.Select(l => l.Description)));
        return new PricingResult(breakdown, new Dictionary<string, string[]>());
    }

    private PricedLine? PriceLine(DraftLineInput input, string path, MenuSnapshot menu, Action<string, string> error)
    {
        var ok = true;
        var size = menu.FindSize(input.SizeCode);
        if (size is null)
        {
            error($"{path}.sizeCode", string.IsNullOrWhiteSpace(input.SizeCode)
                ? "Please choose a size."
                : $"'{input.SizeCode}' isn't a size on our menu.");
            ok = false;
        }

        if (input.Quantity < 1 || input.Quantity > settings.MaxQuantityPerLine)
        {
            error($"{path}.quantity", $"Quantity must be between 1 and {settings.MaxQuantityPerLine}.");
            ok = false;
        }

        var toppings = new List<ToppingInfo>();
        foreach (var code in input.ToppingCodes ?? [])
        {
            var topping = menu.FindTopping(code);
            if (topping is null)
            {
                error($"{path}.toppingCodes", $"'{code}' isn't a topping on our menu.");
                ok = false;
            }
            else if (toppings.Contains(topping))
            {
                error($"{path}.toppingCodes", $"{topping.Name} is already on this pizza.");
                ok = false;
            }
            else
            {
                toppings.Add(topping);
            }
        }

        if (!ok || size is null)
        {
            return null;
        }

        var priced = toppings
            .Select(t => new PricedTopping(t.Code, t.Name, menu.CategoryOf(t).Code, menu.ToppingPrice(size, t)))
            .ToList();
        var unitPrice = size.BasePrice + priced.Sum(t => t.Price);
        return new PricedLine(
            size.Code,
            size.Name,
            input.Quantity,
            size.BasePrice,
            priced,
            unitPrice,
            unitPrice * input.Quantity,
            OrderDescription.Line(input.Quantity, size.Name, priced.Select(t => t.Name)));
    }

    private sealed record ParsedTip(bool IsPercent, decimal Value);

    private ParsedTip? ParseTip(TipInput? input, Action<string, string> error)
    {
        if (input is null)
        {
            return null;
        }

        var isPercent = string.Equals(input.Mode, "percent", StringComparison.OrdinalIgnoreCase);
        if (!isPercent && !string.Equals(input.Mode, "amount", StringComparison.OrdinalIgnoreCase))
        {
            error("tip.mode", "Choose a tip as an amount or a percentage.");
            return null;
        }

        if (!decimal.TryParse(input.Value?.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            error("tip.value", isPercent ? "Enter the tip as a percentage, like 15." : "Enter the tip as an amount, like 2.50.");
            return null;
        }

        if (!Money.HasAtMostTwoDecimals(value))
        {
            error("tip.value", "A tip can have at most 2 decimal places.");
            return null;
        }

        if (isPercent && value > settings.MaxTipPercent)
        {
            error("tip.value", $"A tip percentage must be between 0 and {settings.MaxTipPercent:0}.");
            return null;
        }

        if (!isPercent && value > settings.MaxTipAmount)
        {
            error("tip.value", $"A tip can't be more than ${settings.MaxTipAmount:0.00}.");
            return null;
        }

        return new ParsedTip(isPercent, value);
    }

    private static PricingResult Fail(Dictionary<string, List<string>> errors) =>
        new(null, errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
}
