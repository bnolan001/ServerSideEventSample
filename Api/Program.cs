using System.Runtime.CompilerServices;
using BNolan.RandomSelection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<Selector<string>>(_ =>
{
    var weather = new Selector<string>();

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

builder.Services.AddScoped<Selector<decimal>>(_ =>
{
    var weather = new Selector<decimal>();

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


app.MapGet("/weatherforecast", async ([FromServices] Selector<string> weatherSelector, HttpContext context, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    async IAsyncEnumerable<NotificationEvent> GetNotificationsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return new NotificationEvent
            (
                Guid.NewGuid(),
                weatherSelector.RandomSelect(1).First().Value!
            );
            await Task.Delay(3000, cancellationToken);
        }
    }

    await using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
    await foreach (var notification in GetNotificationsAsync(cancellationToken))
    {
        await writer.WriteAsync($"data: {notification.Message}\n\n");
        await writer.FlushAsync();
    }

})
.WithName("GetWeatherForecast");

app.MapGet("/number/{location}", async ([FromRoute] string location, [FromServices] Selector<int> intSelector, [FromServices] Selector<decimal> decimalSelector, HttpContext context, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    async IAsyncEnumerable<NotificationEvent> GetNotificationsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (location == "1")
            {
                yield return new NotificationEvent
                (
                    Guid.NewGuid(),
                    intSelector.RandomSelect(1).First().Value!.ToString()
                );
                await Task.Delay(2200, cancellationToken);
            }
            if (location == "2.2")
            {
                yield return new NotificationEvent
                (
                    Guid.NewGuid(),
                    decimalSelector.RandomSelect(1).First().Value!.ToString()
                );

                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    await using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
    await foreach (var notification in GetNotificationsAsync(cancellationToken))
    {
        await writer.WriteAsync($"data: {notification.Message}\n\n");
        await writer.FlushAsync();
    }

})
.WithName("GetNumber");

app.Run();

