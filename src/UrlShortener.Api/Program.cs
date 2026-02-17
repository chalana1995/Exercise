using Microsoft.AspNetCore.Mvc;
using Serilog;
using StackExchange.Redis;
using UrlShortener.Api.Services;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// 2. Services
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

builder.Services.AddSingleton<RedisIdGenerator>();
builder.Services.AddSingleton<ShardedUrlRepository>();
builder.Services.AddSingleton<UrlShortenerService>();

// 3. Metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

// 4. Middleware
app.UseSerilogRequestLogging();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// 5. Endpoints

// Health Check
app.MapGet("/health", () => Results.Ok("Healthy"));

// Create Short URL
app.MapPost("/api/v1/shorten", async ([FromBody] CreateUrlRequest request, UrlShortenerService service) =>
{
    if (!Uri.IsWellFormedUriString(request.LongUrl, UriKind.Absolute))
        return Results.BadRequest("Invalid URL format");

    try 
    {
        var result = await service.ShortenUrlAsync(request.LongUrl, request.ExpiresInDays);
        return Results.Created($"/{result.ShortCode}", result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error shortening URL");
        return Results.StatusCode(500);
    }
});

// Redirect
app.MapGet("/{code}", async (string code, UrlShortenerService service) =>
{
    var originalUrl = await service.GetOriginalUrlAsync(code);
    if (originalUrl == null)
        return Results.NotFound();

    // Async click tracking (fire and forget)
    _ = service.TrackClickAsync(code);

    return Results.Redirect(originalUrl);
});

app.Run();

public record CreateUrlRequest(string LongUrl, int? ExpiresInDays);
public record ShortUrlResult(string ShortCode, string ShortUrl, DateTime ExpiresAt);
