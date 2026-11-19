using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Order.Tests;

/// <summary>
/// A tiny stand-in for Restaurant Service that runs on a real port, so the
/// Order Service under test makes a real HTTP call to it.
///
///   GET /restaurants/r1    -> 200 with menu items m101, m102
///   GET /restaurants/slow  -> waits 5 seconds (used to test the timeout)
///   anything else          -> 404
///
/// It also remembers every X-Correlation-ID it receives.
/// </summary>
public class FakeRestaurantServer : IDisposable
{
    private readonly WebApplication _app;
    private readonly List<string> _receivedCorrelationIds = new List<string>();
    private readonly object _lock = new object();

    public string Url { get; }

    public FakeRestaurantServer()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // port 0 = pick any free port
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/restaurants/{id}", async (string id, HttpContext context) =>
        {
            string correlationId = context.Request.Headers["X-Correlation-ID"].ToString();
            lock (_lock)
            {
                _receivedCorrelationIds.Add(correlationId);
            }

            if (id == "slow")
            {
                await Task.Delay(5000);
            }

            if (id == "r1")
            {
                return Results.Json(new
                {
                    id = "r1",
                    name = "Test Restaurant",
                    cuisine = "Test",
                    menu = new[]
                    {
                        new { id = "m101", name = "Item One", price = 100.0 },
                        new { id = "m102", name = "Item Two", price = 200.0 }
                    }
                });
            }

            return Results.NotFound();
        });

        _app.StartAsync().Wait();

        // Find out which port was actually chosen.
        IServer server = _app.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()!;
        Url = addresses.Addresses.First();
    }

    public bool ReceivedCorrelationId(string correlationId)
    {
        lock (_lock)
        {
            return _receivedCorrelationIds.Contains(correlationId);
        }
    }

    public void Dispose()
    {
        _app.StopAsync().Wait();
    }
}
