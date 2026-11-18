using PizzaOrderDesigner.Api.Orders;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Tests.Orders;

public class OrderStatusFlowTests
{
    [Fact]
    public void The_flow_is_strictly_linear_and_ends_at_Delivered()
    {
        Assert.Equal(OrderStatus.Started, OrderStatusFlow.Next(OrderStatus.NotStarted));
        Assert.Equal(OrderStatus.ReadyForPickup, OrderStatusFlow.Next(OrderStatus.Started));
        Assert.Equal(OrderStatus.Delivered, OrderStatusFlow.Next(OrderStatus.ReadyForPickup));
        Assert.Null(OrderStatusFlow.Next(OrderStatus.Delivered));
    }

    [Theory]
    [InlineData(OrderStatus.NotStarted, true)]
    [InlineData(OrderStatus.Started, false)]
    [InlineData(OrderStatus.ReadyForPickup, false)]
    [InlineData(OrderStatus.Delivered, false)]
    public void Only_a_not_started_order_can_be_changed_or_cancelled(OrderStatus status, bool allowed)
    {
        Assert.Equal(allowed, OrderStatusFlow.CustomerCanModify(status));
    }

    [Fact]
    public void Every_status_is_reachable_from_NotStarted()
    {
        var seen = new List<OrderStatus> { OrderStatus.NotStarted };
        for (var s = OrderStatusFlow.Next(OrderStatus.NotStarted); s is not null; s = OrderStatusFlow.Next(s.Value))
        {
            seen.Add(s.Value);
        }

        Assert.Equal(Enum.GetValues<OrderStatus>(), seen);
    }
}
