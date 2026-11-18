using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PizzaOrderDesigner.Api.Menu;
using PizzaOrderDesigner.Api.Pricing;

namespace PizzaOrderDesigner.Api.Tests.Api;

/// <summary>
/// The quote endpoint over real HTTP (routing, JSON, ProblemDetails), with the menu supplied from the seed data
/// so no database is needed.
/// </summary>
public class QuoteEndpointTests(QuoteEndpointTests.Factory factory) : IClassFixture<QuoteEndpointTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:PizzaDb", "Host=unused");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IMenuProvider>(new SeedMenuProvider())));
        }

        private sealed class SeedMenuProvider : IMenuProvider
        {
            public Task<MenuSnapshot> GetMenuAsync(CancellationToken cancellationToken) => Task.FromResult(TestMenu.FromSeed());
        }
    }

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Quote_returns_the_breakdown_with_money_as_two_decimal_numbers()
    {
        var response = await client.PostAsJsonAsync("/api/orders/quote", new
        {
            lines = new[] { new { sizeCode = "large", toppingCodes = new[] { "pepperoni", "bacon" }, quantity = 2 } },
            promoCode = "ILOVEPIZZA",
            tip = new { mode = "percent", value = "15" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(31.23m, doc.RootElement.GetProperty("total").GetDecimal());
        Assert.Contains("\"total\":31.23", json);      // camelCase, a plain number
        Assert.Contains("\"discount\":2.86", json);
        Assert.Equal("PreTax", doc.RootElement.GetProperty("tipBase").GetString());
    }

    [Fact]
    public async Task Zero_amounts_are_still_sent_with_two_decimals()
    {
        var response = await client.PostAsJsonAsync("/api/orders/quote", new
        {
            lines = new[] { new { sizeCode = "small", toppingCodes = Array.Empty<string>(), quantity = 1 } },
        });
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"discount\":0.00", json);
        Assert.Contains("\"tip\":0.00", json);
        Assert.Contains("\"total\":5.33", json); // 5.00 + 0.325 → 0.33 tax
    }

    [Theory]
    [InlineData("5", "5.00")]
    [InlineData("2.5", "2.50")]
    public async Task Amount_tips_are_sent_with_two_decimals(string typed, string expected)
    {
        var response = await client.PostAsJsonAsync("/api/orders/quote", new
        {
            lines = new[] { new { sizeCode = "small", toppingCodes = Array.Empty<string>(), quantity = 1 } },
            tip = new { mode = "amount", value = typed },
        });
        Assert.Contains($"\"tip\":{expected}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_json_gets_the_same_friendly_problem_shape()
    {
        var response = await client.PostAsync("/api/orders/quote",
            new StringContent("{\"lines\": [{\"sizeCode\": \"small\", \"quantity\": \"lots\"}]}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("request.invalid", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("We couldn't read that request.", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_order_is_a_400_problem_with_field_errors_code_and_trace_id()
    {
        var response = await client.PostAsJsonAsync("/api/orders/quote", new
        {
            lines = new[] { new { sizeCode = "small", toppingCodes = Array.Empty<string>(), quantity = 1 } },
            promoCode = "SAVE5",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("Please check your order.", root.GetProperty("title").GetString());
        Assert.Equal("order.invalid", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.Equal("SAVE5 needs an order over $20.00 before tax.",
            root.GetProperty("errors").GetProperty("promoCode")[0].GetString());
    }

    [Fact]
    public async Task Menu_endpoint_returns_sizes_and_per_size_topping_prices()
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/menu"));
        var premium = doc.RootElement.GetProperty("toppingCategories")[1];
        Assert.Equal("premium", premium.GetProperty("code").GetString());
        Assert.Equal(1.32m, premium.GetProperty("toppings")[0].GetProperty("pricesBySize").GetProperty("large").GetDecimal());
    }
}
