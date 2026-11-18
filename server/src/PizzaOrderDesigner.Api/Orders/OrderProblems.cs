using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Orders;

/// <summary>
/// A failed order operation: HTTP status, a stable machine-readable code, and customer-facing wording (#22).
/// Field errors and a fresh quote are attached when relevant.
/// </summary>
public sealed record OrderProblem(
    int Status,
    string Code,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    PriceBreakdown? Quote = null);

/// <summary>All customer-facing wording for order errors lives here, so it stays consistent.</summary>
public static class OrderProblems
{
    public static OrderProblem KeyMissing() => new(400, "idempotency.key_missing",
        "We couldn't send your order.", "The request is missing its Idempotency-Key header.");

    public static OrderProblem Invalid(IReadOnlyDictionary<string, string[]> errors) => new(400, "order.invalid",
        "Please check your order.", "Some items in your order need attention.", errors);

    public static OrderProblem NotFound() => new(404, "order.not_found",
        "We couldn't find that order.", "It may have been cancelled.");

    public static OrderProblem KeyReused() => new(422, "idempotency.key_reused",
        "This request doesn't match the order it was first sent with.",
        "The Idempotency-Key was already used for a different order. Start a new checkout to place another order.");

    public static OrderProblem Cancelled() => new(409, "order.cancelled",
        "This order was cancelled.", "It won't be sent to the kitchen. Start a new order if you'd like to order again.");

    public static OrderProblem PriceChanged(PriceBreakdown quote) => new(409, "price.changed",
        "The price of your order has changed.", $"The new total is {Money.Format(quote.Total)}. Please review and confirm.",
        Quote: quote);

    public static OrderProblem AlreadyStarted() => new(409, "order.already_started",
        "The kitchen has already started your order.", "It can no longer be changed or cancelled.");

    public static OrderProblem AlreadyDelivered() => new(409, "order.already_delivered",
        "This order has already been delivered.");

    public static OrderProblem StatusChanged() => new(409, "order.status_changed",
        "This order changed while we were updating it.", "Refresh and try again.");

    public static OrderProblem VersionMismatch() => new(412, "order.version_mismatch",
        "This order was changed somewhere else.", "Refresh to see the latest version, then try again.");

    public static OrderProblem PreconditionRequired() => new(428, "order.precondition_required",
        "We couldn't update your order.", "The request is missing its If-Match header (the order's ETag).");
}
