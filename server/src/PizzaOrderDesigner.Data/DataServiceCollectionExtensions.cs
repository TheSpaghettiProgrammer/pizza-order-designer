using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PizzaOrderDesigner.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PizzaDbContext"/> against PostgreSQL. Migrations live in this (Data) assembly.
    /// </summary>
    public static IServiceCollection AddPizzaData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PizzaDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(PizzaDbContext).Assembly.FullName)));

        return services;
    }
}
