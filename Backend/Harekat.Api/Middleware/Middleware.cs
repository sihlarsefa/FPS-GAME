using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Harekat.Application.Common;

namespace Harekat.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = ex.ErrorCode,
                message = ex.Message
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "internal",
                message = "Beklenmeyen sunucu hatası."
            }));
        }
    }
}

/// <summary>Basit IP başına sabit pencere rate limit.</summary>
public sealed class SimpleRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, Window> Windows = new();
    private readonly RequestDelegate _next;
    private readonly int _limit;
    private readonly TimeSpan _window;

    public SimpleRateLimitMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _limit = int.TryParse(config["RateLimit:PermitLimit"], out var l) ? l : 120;
        _window = TimeSpan.FromSeconds(int.TryParse(config["RateLimit:WindowSeconds"], out var s) ? s : 60);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTimeOffset.UtcNow;
        var win = Windows.AddOrUpdate(key,
            _ => new Window(now, 1),
            (_, existing) =>
            {
                if (now - existing.Started > _window)
                    return new Window(now, 1);
                return existing with { Count = existing.Count + 1 };
            });

        if (win.Count > _limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = ((int)_window.TotalSeconds).ToString();
            await context.Response.WriteAsJsonAsync(new { error = ErrorCodes.RateLimited, message = "Çok fazla istek." });
            return;
        }

        await _next(context);
    }

    private sealed record Window(DateTimeOffset Started, int Count);
}

public static class HttpContextExtensions
{
    public static Guid GetPlayerId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var id))
            throw new AppException("Kimlik doğrulanamadı.", 401, ErrorCodes.Unauthorized);
        return id;
    }
}
