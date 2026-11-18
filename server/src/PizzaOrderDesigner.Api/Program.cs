using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Orders;
using PizzaOrderDesigner.Api.Pricing;
using PizzaOrderDesigner.Api.Pricing.Promotions;
using PizzaOrderDesigner.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PizzaDb")
    ?? throw new InvalidOperationException("Connection string 'PizzaDb' is not configured.");

builder.Services.AddPizzaData(connectionString);
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.SectionName));
builder.Services.AddScoped<IMenuProvider, EfMenuProvider>();
builder.Services.AddSingleton<IPromotionRule, PercentOffOrderRule>();
builder.Services.AddSingleton<IPromotionRule, AmountOffOverThresholdRule>();
builder.Services.AddSingleton<PromotionEngine>();
builder.Services.AddSingleton<OrderPricer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OrderService>();
builder.Services.Configure<KitchenAutoAdvanceOptions>(builder.Configuration.GetSection(KitchenAutoAdvanceOptions.SectionName));
builder.Services.AddHostedService<KitchenAutoAdvanceService>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
{
    // Unhandled exceptions (#22): a friendly message and the traceId for support. The exception handler middleware
    // logs the full exception, with the same traceId, on the server. Stack traces never reach the client.
    if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
    {
        context.ProblemDetails.Title = "Something went wrong on our side.";
        context.ProblemDetails.Detail = "Please try again. If it keeps happening, show staff this reference code.";
        context.ProblemDetails.Extensions["code"] = "server.error";
    }
});
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.AllowInputFormatterExceptionMessages = false; // don't echo .NET type names back to clients
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
    {
        // Malformed JSON or wrong types: same friendly ProblemDetails shape as our own validation errors.
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest,
                title: "We couldn't read that request.",
                detail: "Some values are missing or in the wrong format.");
        problem.Extensions["code"] = "request.invalid";
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PizzaDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // bodiless errors (e.g. a non-GUID id → 404) also get a ProblemDetails body

// Apply pending EF Core migrations at startup when enabled (Development and docker-compose).
// See docs/DECISIONS.md: a dedicated migration step would be preferred for production.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PizzaDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/api/health");
app.MapControllers();

app.Run();

/// <summary>Exposed for WebApplicationFactory in the API tests.</summary>
public partial class Program;
