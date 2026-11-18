using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PizzaOrderDesigner.Api.Orders;
using PizzaOrderDesigner.Data;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Tests.Orders;

/// <summary>
/// #20, deterministically: the customer's cancel and the kitchen's start are each one conditional statement, so the
/// database's row lock decides the winner. These tests hold one side's lock open in a transaction and prove the
/// other side waits for it, then re-checks its WHERE clause and loses cleanly. (A plain parallel-request race test
/// can't guarantee both orderings ever happen; this one forces each.)
/// </summary>
[Collection("postgres")]
public class RaceGuaranteeTests(PostgresApiFactory factory)
{
    private async Task<Guid> NewOrderId()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                lines = new[] { new { sizeCode = "small", toppingCodes = Array.Empty<string>(), quantity = 1 } },
                expectedTotal = 5.33m,
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Kitchen_start_holding_the_lock_makes_a_concurrent_cancel_fail_with_already_started()
    {
        var id = await NewOrderId();

        using var kitchenScope = factory.Services.CreateScope();
        var kitchenDb = kitchenScope.ServiceProvider.GetRequiredService<PizzaDbContext>();
        await using var tx = await kitchenDb.Database.BeginTransactionAsync();
        var started = await kitchenDb.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.NotStarted)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Started));
        Assert.Equal(1, started); // the kitchen now holds the row lock, uncommitted

        using var customerScope = factory.Services.CreateScope();
        var cancel = Task.Run(() => customerScope.ServiceProvider.GetRequiredService<OrderService>().CancelAsync(id, CancellationToken.None));
        await Task.Delay(500);
        Assert.False(cancel.IsCompleted, "the cancel should be waiting on the kitchen's row lock");

        await tx.CommitAsync();
        var problem = await cancel;

        Assert.NotNull(problem);
        Assert.Equal("order.already_started", problem.Code);
        Assert.True(await kitchenDb.Orders.AnyAsync(o => o.Id == id && o.Status == OrderStatus.Started));
    }

    [Fact]
    public async Task Cancel_holding_the_lock_makes_a_concurrent_kitchen_start_fail_with_not_found()
    {
        var id = await NewOrderId();

        using var customerScope = factory.Services.CreateScope();
        var customerDb = customerScope.ServiceProvider.GetRequiredService<PizzaDbContext>();
        await using var tx = await customerDb.Database.BeginTransactionAsync();
        var deleted = await customerDb.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.NotStarted)
            .ExecuteDeleteAsync();
        Assert.Equal(1, deleted); // the cancel holds the row lock, uncommitted

        using var kitchenScope = factory.Services.CreateScope();
        var start = Task.Run(() => kitchenScope.ServiceProvider.GetRequiredService<OrderService>().AdvanceAsync(id, "test", CancellationToken.None));
        await Task.Delay(500);
        Assert.False(start.IsCompleted, "the kitchen start should be waiting on the cancel's row lock");

        await tx.CommitAsync();
        var result = await start;

        Assert.Null(result.Order);
        Assert.Equal("order.not_found", result.Problem!.Code);
        Assert.False(await customerDb.Orders.AnyAsync(o => o.Id == id));
    }
}
