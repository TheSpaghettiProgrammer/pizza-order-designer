using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IMenuProvider menuProvider, OrderPricer pricer) : ControllerBase
{
    /// <summary>
    /// Prices a draft order without saving it. The order builder calls this on every change and only displays
    /// what comes back (D-039), so the running total is exactly what submit will charge.
    /// </summary>
    [HttpPost("quote")]
    [ProducesResponseType<PriceBreakdown>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PriceBreakdown>> Quote(OrderDraft draft, CancellationToken cancellationToken)
    {
        var menu = await menuProvider.GetMenuAsync(cancellationToken);
        var result = pricer.Price(draft, menu);
        return result.IsValid ? result.Breakdown! : InvalidOrder(result.Errors);
    }

    private ActionResult InvalidOrder(IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        var problem = ProblemDetailsFactory.CreateValidationProblemDetails(
            HttpContext, modelState, StatusCodes.Status400BadRequest,
            title: "Please check your order.",
            detail: "Some items in your order need attention.");
        problem.Extensions["code"] = "order.invalid";
        return BadRequest(problem);
    }
}
