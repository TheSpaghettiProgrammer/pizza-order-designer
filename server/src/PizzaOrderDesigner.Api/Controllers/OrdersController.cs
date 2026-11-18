using Microsoft.AspNetCore.Mvc;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Orders;
using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Controllers;

/// <summary>The order service API (docs/API.md).</summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IMenuProvider menuProvider, OrderPricer pricer, OrderService orders) : OrderApiControllerBase
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
        return result.IsValid ? result.Breakdown! : Problem(OrderProblems.Invalid(result.Errors));
    }

    /// <summary>Submits an order. Safe to retry with the same Idempotency-Key (#16).</summary>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        OrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await orders.SubmitAsync(idempotencyKey, request, cancellationToken);
        if (result.Problem is not null)
        {
            return Problem(result.Problem);
        }

        var body = WithETag(result.Order!);
        return result.Created ? CreatedAtAction(nameof(Get), new { id = result.Order!.Id }, body) : Ok(body);
    }

    /// <summary>The order and its status (#17). Safe to poll.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await orders.GetAsync(id, cancellationToken));

    /// <summary>Replaces the order while it is NotStarted (#18). Requires If-Match with the ETag from GET.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        OrderRequest request,
        CancellationToken cancellationToken) =>
        ToResponse(await orders.UpdateAsync(id, ifMatch, request, cancellationToken));

    /// <summary>Cancels (deletes) the order while it is NotStarted (#19, D-040).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        await orders.CancelAsync(id, cancellationToken) is { } problem ? Problem(problem) : NoContent();
}

/// <summary>Restaurant side (#21, D-042): advance an order to its next status.</summary>
[ApiController]
[Route("api/kitchen/orders")]
public sealed class KitchenController(OrderService orders) : OrderApiControllerBase
{
    [HttpPost("{id:guid}/advance")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Advance(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await orders.AdvanceAsync(id, "kitchen endpoint", cancellationToken));
}

/// <summary>Maps order results to HTTP: one place for status codes and the ProblemDetails shape (#22).</summary>
public abstract class OrderApiControllerBase : ControllerBase
{
    protected IActionResult ToResponse(OrderResult result) =>
        result.Problem is not null ? Problem(result.Problem) : Ok(WithETag(result.Order!));

    protected OrderResponse WithETag(PizzaOrderDesigner.Data.Orders.Order order)
    {
        Response.Headers.ETag = OrderService.ETag(order);
        return OrderResponse.From(order);
    }

    protected ObjectResult Problem(OrderProblem problem)
    {
        var details = problem.Errors is null
            ? ProblemDetailsFactory.CreateProblemDetails(HttpContext, problem.Status, problem.Title, detail: problem.Detail)
            : ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext, ToModelState(problem.Errors), problem.Status, problem.Title, detail: problem.Detail);
        details.Extensions["code"] = problem.Code;
        if (problem.Quote is not null)
        {
            details.Extensions["quote"] = problem.Quote;
        }

        return new ObjectResult(details) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ToModelState(IReadOnlyDictionary<string, string[]> errors)
    {
        var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                state.AddModelError(field, message);
            }
        }

        return state;
    }
}
