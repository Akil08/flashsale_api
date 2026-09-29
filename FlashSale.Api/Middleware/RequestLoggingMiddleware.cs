using System.Diagnostics;
using System.Security.Claims;

namespace FlashSale.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Process the request down the pipeline
        await _next(context);
        
        stopwatch.Stop();
        
        // Try to get the User ID from JWT claims, fallback to "Anonymous"
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? context.User.Identity?.Name 
                     ?? "Anonymous";

        _logger.LogInformation(
            "Request: {Method} {Path} | Status: {StatusCode} | Duration: {DurationMs}ms | User: {UserId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            userId);
    }
}
