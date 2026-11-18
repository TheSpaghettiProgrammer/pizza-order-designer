using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Orders;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Data;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Tests.Orders;

/// <summary>
/// Forces the interleavings a parallel-request test can't reliably produce: something changes the order between an
/// update's read and its save, or between an advance's read and its write. The seam is the menu provider, which
/// UpdateAsync calls after reading the order and before saving.
/// </summary>
[Collection("postgres")]
public class InterleavingTests(PostgresApiFactory factory)
{
    private sealed class HookedMenuProvider(Func<Task> beforeReturning) : IMenuProvider
    {
        public async Task<MenuSnapshot> GetMenuAsync(CancellationToken cancellationToken)
        {
            await beforeReturning();
            return TestMenu.FromSeed();
        }
    }

    private async Task<(Guid Id, string ETag)> NewOrder()
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
        return (doc.RootElement.GetProperty("id").GetGuid(), response.Headers.ETag!.Tag);
    }

    private static OrderRequest TwoSmalls() => new()
    {
        Lines = [new DraftLineInput { SizeCode = "small", ToppingCodes = [], Quantity = 2 }],
        ExpectedTotal = 10.65m, // 10.00 + 0.65 tax
    };

    private OrderService ServiceWithHook(IServiceScope scope, Func<Task> hook) => new(
        scope.ServiceProvider.GetRequiredService<PizzaDbContext>(),
        new HookedMenuProvider(hook),
        scope.ServiceProvider.GetRequiredService<OrderPricer>(),
        TimeProvider.System,
        scope.ServiceProvider.GetRequiredService<ILogger<OrderService>>());

    private async Task<T> InOtherScope<T>(Func<OrderService, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<OrderService>());
    }

    [Fact]
    public async Task Update_that_loses_to_a_cancel_returns_404_not_500()
    {
        var (id, etag) = await NewOrder();
        using var scope = factory.Services.CreateScope();
        var service = ServiceWithHook(scope, async () => Assert.Null(await InOtherScope(s => s.CancelAsync(id, default))));

        var result = await service.UpdateAsync(id, etag, TwoSmalls(), default);

        Assert.Equal("order.not_found", result.Problem?.Code);
    }

    [Fact]
    public async Task Update_that_loses_to_the_kitchen_start_returns_409_already_started()
    {
        var (id, etag) = await NewOrder();
        using var scope = factory.Services.CreateScope();
        var service = ServiceWithHook(scope, async () => Assert.NotNull((await InOtherScope(s => s.AdvanceAsync(id, "test", default))).Order));

        var result = await service.UpdateAsync(id, etag, TwoSmalls(), default);

        Assert.Equal("order.already_started", result.Problem?.Code);
        using var checkScope = factory.Services.CreateScope();
        var order = await checkScope.ServiceProvider.GetRequiredService<PizzaDbContext>()
            .Orders.Include(o => o.Lines).SingleAsync(o => o.Id == id);
        Assert.Equal(1, order.Lines.Single().Quantity); // the losing update changed nothing
    }

    [Fact]
    public async Task A_stale_kitchen_advance_does_not_double_step()
    {
        // Kitchen A holds NotStarted → Started uncommitted. Kitchen B read NotStarted too, so its UPDATE … WHERE
        // status = 'NotStarted' waits, re-checks after A commits, matches nothing, and reports the change (409).
        // Without the status predicate B would also "succeed", so a slower duplicate could overwrite newer state.
        var (id, _) = await NewOrder();
        using var scopeA = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<PizzaDbContext>();
        await using var tx = await dbA.Database.BeginTransactionAsync();
        Assert.Equal(1, await dbA.Orders.Where(o => o.Id == id && o.Status == OrderStatus.NotStarted)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Started)));

        var advanceB = Task.Run(() => InOtherScope(s => s.AdvanceAsync(id, "kitchen B", default)));
        await Task.Delay(500);
        Assert.False(advanceB.IsCompleted, "kitchen B should be waiting on kitchen A's row lock");

        await tx.CommitAsync();
        var result = await advanceB;

        Assert.Equal("order.status_changed", result.Problem?.Code);
        Assert.True(await dbA.Orders.AnyAsync(o => o.Id == id && o.Status == OrderStatus.Started));
    }
}
