using Microsoft.EntityFrameworkCore;
using PizzaOrderDesigner.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PizzaDb")
    ?? throw new InvalidOperationException("Connection string 'PizzaDb' is not configured.");

builder.Services.AddPizzaData(connectionString);
builder.Services.AddControllers();
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
