using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Data;
using PizzaOrderDesigner.Data.Orders;

namespace PizzaOrderDesigner.Api.Orders;

/// <summary>
/// The order lifecycle. Guarantees (docs/plans/pr-b-order-service.md):
/// <list type="bullet">
/// <item>One order per Idempotency-Key, whatever the retry timing, and a cancelled order is never re-created.</item>
/// <item>What the customer saw is what they are charged: submit and update re-price and reject a mismatch.</item>
/// <item>Cancel and the kitchen's "start" can't both succeed: each is a single conditional SQL statement.</item>
/// </list>
/// </summary>
public sealed class OrderService(
    PizzaDbContext db,
    IMenuProvider menuProvider,
    OrderPricer pricer,
    TimeProvider clock,
    ILogger<OrderService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- submit (#16)

    public async Task<OrderResult> SubmitAsync(string? idempotencyKey, OrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return OrderResult.Fail(OrderProblems.KeyMissing());
        }

        if (idempotencyKey.Length > 100)
        {
            return OrderResult.Fail(OrderProblems.KeyTooLong());
        }

        var hash = RequestHash(request);
        var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == idempotencyKey, ct);
        if (existing is not null)
        {
            return await ReplayAsync(existing, hash, ct);
        }

        var (breakdown, problem) = await PriceAsync(request, ct);
        if (problem is not null)
        {
            return OrderResult.Fail(problem);
        }

        var now = clock.GetUtcNow();
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.NotStarted, CreatedAt = now, UpdatedAt = now, PricingJson = "{}" };
        Apply(order, request, breakdown!);
        db.Orders.Add(order);
        db.IdempotencyRecords.Add(new IdempotencyRecord { Key = idempotencyKey, RequestHash = hash, OrderId = order.Id, CreatedAt = now });

        try
        {
            await db.SaveChangesAsync(ct); // one transaction: the order, its lines and the idempotency record
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent request with the same key committed first. Return its order instead of a second one.
            db.ChangeTracker.Clear();
            logger.LogInformation("Concurrent submit for idempotency key {Key} lost the race; replaying the winner", idempotencyKey);
            var winner = await db.IdempotencyRecords.AsNoTracking().FirstAsync(r => r.Key == idempotencyKey, ct);
            return await ReplayAsync(winner, hash, ct);
        }

        logger.LogInformation("Order {OrderId} submitted, total {Total}, key {Key}", order.Id, order.Total, idempotencyKey);
        return OrderResult.Ok(order, created: true);
    }

    private async Task<OrderResult> ReplayAsync(IdempotencyRecord record, string hash, CancellationToken ct)
    {
        if (record.RequestHash != hash)
        {
            logger.LogWarning("Idempotency key {Key} reused with a different request", record.Key);
            return OrderResult.Fail(OrderProblems.KeyReused());
        }

        var order = await LoadAsync(record.OrderId, ct);
        if (order is null)
        {
            logger.LogInformation("Retry for key {Key} refers to cancelled order {OrderId}; not re-creating it", record.Key, record.OrderId);
            return OrderResult.Fail(OrderProblems.Cancelled());
        }

        logger.LogInformation("Idempotent replay for key {Key} returned order {OrderId}", record.Key, order.Id);
        return OrderResult.Ok(order);
    }

    // ---------------------------------------------------------------- read (#17)

    public async Task<OrderResult> GetAsync(Guid id, CancellationToken ct) =>
        await LoadAsync(id, ct) is { } order ? OrderResult.Ok(order) : OrderResult.Fail(OrderProblems.NotFound());

    // ---------------------------------------------------------------- update (#18)

    public async Task<OrderResult> UpdateAsync(Guid id, string? ifMatch, OrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return OrderResult.Fail(OrderProblems.PreconditionRequired());
        }

        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return OrderResult.Fail(OrderProblems.NotFound());
        }

        // Status first: an edit after the kitchen started is "already started" (409), even though the kitchen's
        // advance also made the customer's ETag stale. Same order of checks as the conflict handler below.
        if (!OrderStatusFlow.CustomerCanModify(order.Status))
        {
            return OrderResult.Fail(OrderProblems.AlreadyStarted());
        }

        if (!TryParseETag(ifMatch, out var version) || version != order.Version)
        {
            return OrderResult.Fail(OrderProblems.VersionMismatch());
        }

        var (breakdown, problem) = await PriceAsync(request, ct);
        if (problem is not null)
        {
            return OrderResult.Fail(problem);
        }

        Apply(order, request, breakdown!);
        order.UpdatedAt = clock.GetUtcNow();
        try
        {
            // The UPDATE is conditional on the xmin we read: if the kitchen (or anyone) changed the row since, it fails.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsForeignKeyViolation(ex))
        {
            // Lost a race: the kitchen advanced it (xmin changed → concurrency exception), or the customer cancelled
            // it (the order row is gone, so inserting the new lines violates their foreign key). Rolled back; re-read.
            db.ChangeTracker.Clear();
            var current = await LoadAsync(id, ct);
            return OrderResult.Fail(current is null ? OrderProblems.NotFound()
                : !OrderStatusFlow.CustomerCanModify(current.Status) ? OrderProblems.AlreadyStarted()
                : OrderProblems.VersionMismatch());
        }

        logger.LogInformation("Order {OrderId} updated, new total {Total}", order.Id, order.Total);
        return OrderResult.Ok(order);
    }

    // ---------------------------------------------------------------- cancel = delete (#19, #20)

    /// <summary>Deletes the order if, and only if, it is still NotStarted, in one statement. Returns null on success.</summary>
    public async Task<OrderProblem?> CancelAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.NotStarted)
            .ExecuteDeleteAsync(ct);
        if (deleted == 1)
        {
            logger.LogInformation("Order {OrderId} cancelled (deleted)", id);
            return null;
        }

        var exists = await db.Orders.AnyAsync(o => o.Id == id, ct);
        logger.LogInformation("Cancel of order {OrderId} refused: {Reason}", id, exists ? "already started" : "not found");
        return exists ? OrderProblems.AlreadyStarted() : OrderProblems.NotFound();
    }

    // ---------------------------------------------------------------- kitchen (#20, #21)

    /// <summary>Moves the order to its next status with a single conditional UPDATE (the race guarantee).</summary>
    public async Task<OrderResult> AdvanceAsync(Guid id, string actor, CancellationToken ct)
    {
        var current = await db.Orders.Where(o => o.Id == id).Select(o => (OrderStatus?)o.Status).FirstOrDefaultAsync(ct);
        if (current is null)
        {
            return OrderResult.Fail(OrderProblems.NotFound());
        }

        if (OrderStatusFlow.Next(current.Value) is not { } next)
        {
            return OrderResult.Fail(OrderProblems.AlreadyDelivered());
        }

        var now = clock.GetUtcNow();
        var updated = await db.Orders
            .Where(o => o.Id == id && o.Status == current.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, next).SetProperty(o => o.UpdatedAt, now), ct);
        if (updated == 0)
        {
            // Cancelled (deleted) or advanced by someone else between our read and the update.
            var exists = await db.Orders.AnyAsync(o => o.Id == id, ct);
            return OrderResult.Fail(exists ? OrderProblems.StatusChanged() : OrderProblems.NotFound());
        }

        logger.LogInformation("Order {OrderId} {From} -> {To} by {Actor}", id, current.Value, next, actor);
        return OrderResult.Ok((await LoadAsync(id, ct))!);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(PriceBreakdown? Breakdown, OrderProblem? Problem)> PriceAsync(OrderRequest request, CancellationToken ct)
    {
        var menu = await menuProvider.GetMenuAsync(ct);
        var result = pricer.Price(request.ToDraft(), menu);
        if (!result.IsValid)
        {
            return (null, OrderProblems.Invalid(result.Errors));
        }

        if (request.ExpectedTotal is null)
        {
            return (null, OrderProblems.Invalid(new Dictionary<string, string[]>
            {
                ["expectedTotal"] = ["The total you were shown is required."],
            }));
        }

        if (result.Breakdown!.Total != request.ExpectedTotal.Value)
        {
            logger.LogInformation("Price changed: customer saw {Expected}, server priced {Actual}", request.ExpectedTotal, result.Breakdown.Total);
            return (null, OrderProblems.PriceChanged(result.Breakdown));
        }

        return (result.Breakdown, null);
    }

    private static void Apply(Order order, OrderRequest request, PriceBreakdown breakdown)
    {
        order.PromoCode = breakdown.PromoCode;
        order.TipMode = request.Tip?.Mode?.Trim().ToLowerInvariant();
        order.TipValue = request.Tip?.Value?.Trim();
        order.Lines.Clear();
        order.Lines.AddRange(breakdown.Lines.Select((l, i) => new OrderLine
        {
            Position = i,
            SizeCode = l.SizeCode,
            Quantity = l.Quantity,
            ToppingCodes = l.Toppings.Select(t => t.Code).ToList(),
        }));
        order.Subtotal = breakdown.Subtotal;
        order.Discount = breakdown.Discount;
        order.Tax = breakdown.Tax;
        order.Tip = breakdown.Tip;
        order.Total = breakdown.Total;
        order.PricingJson = JsonSerializer.Serialize(breakdown, Json);
    }

    private Task<Order?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    private static string RequestHash(OrderRequest request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json))));

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsForeignKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    public static string ETag(Order order) => $"\"{order.Version.ToString(CultureInfo.InvariantCulture)}\"";

    private static bool TryParseETag(string value, out uint version)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        return uint.TryParse(trimmed.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
