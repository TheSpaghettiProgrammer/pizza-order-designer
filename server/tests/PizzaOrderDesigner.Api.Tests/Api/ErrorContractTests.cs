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

/// <summary>#22: an unexpected failure gives the customer a friendly message and a reference code, nothing internal.</summary>
public class ErrorContractTests(ErrorContractTests.BrokenMenuFactory factory) : IClassFixture<ErrorContractTests.BrokenMenuFactory>
{
    public sealed class BrokenMenuFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:PizzaDb", "Host=unused");
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IMenuProvider>(new ThrowingMenuProvider())));
        }

        private sealed class ThrowingMenuProvider : IMenuProvider
        {
            public Task<MenuSnapshot> GetMenuAsync(CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Simulated database outage: secret-connection-detail");
        }
    }

    [Fact]
    public async Task Unhandled_exception_is_a_friendly_500_problem_with_trace_id_and_no_internals()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders/quote", new
        {
            lines = new[] { new { sizeCode = "small", toppingCodes = Array.Empty<string>(), quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Something went wrong on our side.", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("server.error", doc.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("secret-connection-detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(" at ", body); // no stack frames
    }
}
