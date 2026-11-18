using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Api.Menu;
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
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PizzaDbContext>("database");

var app = builder.Build();

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
