using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BNolan.RandomSelection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
ConcurrentDictionary<string, string> _eventData = new();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<Selector<string>>(_ =>
{
    var weather = new Selector<string>();

    // Seed weather descriptions for random selection.
    weather.TryAddItem("Freezing");
    weather.TryAddItem("Bracing");
    weather.TryAddItem("Chilly");
    weather.TryAddItem("Cool");
    weather.TryAddItem("Mild");
    weather.TryAddItem("Warm");
    weather.TryAddItem("Balmy");
    weather.TryAddItem("Hot");
    weather.TryAddItem("Sweltering");
    weather.TryAddItem("Scorching");

    return weather;
});
builder.Services.AddSingleton<ConcurrentDictionary<string, string>>();
builder.Services.AddSingleton<KeyValueService>(); // Register KeyValueService for SSE handling.
builder.Services.AddScoped<Selector<decimal>>(_ =>
{
    var weather = new Selector<decimal>();

    // Seed decimal values for random selection.
    weather.TryAddItem(1.1m);
    weather.TryAddItem(2.2m);
    weather.TryAddItem(3.3m);
    weather.TryAddItem(4.4m);
    weather.TryAddItem(5.5m);
    weather.TryAddItem(6.6m);
    weather.TryAddItem(7.7m);
    weather.TryAddItem(8.8m);
    weather.TryAddItem(9.9m);
    weather.TryAddItem(10.0m);

    return weather;
});

builder.Services.AddScoped<Selector<int>>(_ =>
{
    var weather = new Selector<int>();

    // Seed integer values for random selection.
    weather.TryAddItem(0);
    weather.TryAddItem(1);
    weather.TryAddItem(2);
    weather.TryAddItem(3);
    weather.TryAddItem(4);
    weather.TryAddItem(5);
    weather.TryAddItem(6);
    weather.TryAddItem(7);
    weather.TryAddItem(8);
    weather.TryAddItem(9);
    weather.TryAddItem(10);

    return weather;
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


// Endpoint to stream random weather forecasts via SSE.
app.MapGet("/weatherforecast", async ([FromServices] Selector<string> weatherSelector, HttpContext context, CancellationToken cancellationToken) =>
{
    // Set headers for Server-Sent Events (SSE).
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    // Generator function to yield notification events periodically.
    async IAsyncEnumerable<NotificationEvent> GetNotificationsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Yield a new random weather notification.
            yield return new NotificationEvent
            (
                Guid.NewGuid(),
                weatherSelector.RandomSelect(1).First().Value!
            );
            await Task.Delay(3000, cancellationToken); // Wait for 3 seconds before the next event.
        }
    }

    // Write events to the response stream.
    await using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
    await foreach (var notification in GetNotificationsAsync(cancellationToken))
    {
        await writer.WriteAsync($"data: {notification.Message}\n\n");
        await writer.FlushAsync();
    }

})
.WithName("GetWeatherForecast");

// Endpoint to stream random numbers based on location identifier via SSE.
app.MapGet("/number/{location}", async ([FromRoute] string location, [FromServices] Selector<int> intSelector, [FromServices] Selector<decimal> decimalSelector, HttpContext context, CancellationToken cancellationToken) =>
{
    // Set headers for SSE.
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    // Generator function for number notifications handling different locations.
    async IAsyncEnumerable<NotificationEvent> GetNotificationsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (location == "1")
            {
                // Yield random integer for location "1".
                yield return new NotificationEvent
                (
                    Guid.NewGuid(),
                    intSelector.RandomSelect(1).First().Value!.ToString()
                );
                await Task.Delay(2200, cancellationToken);
            }
            if (location == "2.2")
            {
                // Yield random decimal for location "2.2".
                yield return new NotificationEvent
                (
                    Guid.NewGuid(),
                    decimalSelector.RandomSelect(1).First().Value!.ToString()
                );

                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    // Write events to the response stream.
    await using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
    await foreach (var notification in GetNotificationsAsync(cancellationToken))
    {
        await writer.WriteAsync($"data: {notification.Message}\n\n");
        await writer.FlushAsync();
    }

})
.WithName("GetNumber");

// Endpoint to trigger a manual event update.
// This supports both 'text/plain'
app.MapPost("/trigger-event", async (HttpRequest request, [FromServices] KeyValueService eventService, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var eventData = await reader.ReadToEndAsync(cancellationToken);

    if (string.IsNullOrWhiteSpace(eventData))
    {
        return Results.BadRequest("Event data is empty.");
    }

    // Expect format "key:value".
    var data = eventData.Split(':');
    if(data.Length != 2)
    {
        return Results.BadRequest($"Invalid event data format. Expected format: 'key:value'. Received: {eventData}");
    }

    // Update the service which triggers the SSE notification.
    eventService.Update(data[0], data[1]);

    return Results.Ok($"Event triggered for {data[0]}");
}).WithName("TriggerEvent");

// Endpoint to subscribe to updates for a specific key via SSE.
app.MapGet("/events/{key}", async ([FromRoute] string key, [FromServices] KeyValueService service, HttpContext context, CancellationToken cancellationToken) =>
{
    // Set headers for SSE.
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    // Write the stream of updates to the response.
    await using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
    await foreach (var message in service.GetNotificationsAsync(key, cancellationToken))
    {
        await writer.WriteAsync($"data: {message}\n\n");
        await writer.FlushAsync();
    }
})
.WithName("GetEvents");

app.Run();

