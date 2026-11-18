using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace PizzaOrderDesigner.Api.Tests.Orders;

/// <summary>
/// The real API against a real, throwaway Postgres (Testcontainers; requires Docker). Migrations run on startup, so
/// tests exercise the actual SQL: unique keys, conditional UPDATE/DELETE, xmin concurrency and cascades.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PizzaDb", postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Kitchen:AutoAdvance:Enabled", "false");
    }
}
