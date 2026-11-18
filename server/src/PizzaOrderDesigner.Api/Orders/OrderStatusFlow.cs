using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Orders;

/// <summary>
/// The only place order status transitions are defined (#20). Strictly linear: no skipping, no going back
/// (D-040). Cancelling is not a status: it deletes the order, and is only allowed while <see cref="OrderStatus.NotStarted"/>.
/// </summary>
public static class OrderStatusFlow
{
    private static readonly Dictionary<OrderStatus, OrderStatus> NextStatus = new()
    {
        [OrderStatus.NotStarted] = OrderStatus.Started,
        [OrderStatus.Started] = OrderStatus.ReadyForPickup,
        [OrderStatus.ReadyForPickup] = OrderStatus.Delivered,
    };

    /// <summary>The next status, or null when the order is already Delivered.</summary>
    public static OrderStatus? Next(OrderStatus current) => NextStatus.TryGetValue(current, out var next) ? next : null;

    /// <summary>The customer may change or cancel an order only before the kitchen starts it.</summary>
    public static bool CustomerCanModify(OrderStatus status) => status == OrderStatus.NotStarted;
}
