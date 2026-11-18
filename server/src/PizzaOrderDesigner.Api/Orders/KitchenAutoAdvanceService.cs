using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PizzaOrderDesigner.Data;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Orders;

/// <summary>
/// Optional kitchen simulation (D-042): when enabled, every interval each order that has sat in its status for at
/// least one interval moves to the next status, through the same conditional advance as the kitchen endpoint, so
/// the cancel-vs-start guarantee holds here too. Off by default; docker-compose turns it on for demos.
/// </summary>
public sealed class KitchenAutoAdvanceService(
    IServiceScopeFactory scopes,
    IOptions<KitchenAutoAdvanceOptions> options,
    TimeProvider clock,
    ILogger<KitchenAutoAdvanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSeconds));
        logger.LogInformation("Kitchen auto-advance is on: every {Interval}", interval);
        using var timer = new PeriodicTimer(interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PizzaDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<OrderService>();
                var cutoff = clock.GetUtcNow() - interval;
                var due = await db.Orders
                    .Where(o => o.Status != OrderStatus.Delivered && o.UpdatedAt <= cutoff)
                    .Select(o => o.Id)
                    .ToListAsync(stoppingToken);
                foreach (var id in due)
                {
                    await service.AdvanceAsync(id, "auto-advance timer", stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Kitchen auto-advance tick failed");
            }
        }
    }
}
