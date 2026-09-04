using Zsc.CommonLib;

namespace Zsc.Interceptor.Middleware;

/// <summary>
/// Middleware that ensures every request has an X-Correlation-Id header.
/// If not present, generates a new GUID-based correlation ID.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationIdKey = "CorrelationId";

        string correlationId;
        if (context.Request.Headers.TryGetValue(CorrelationIdConstants.HeaderName, out var correlationIdHeader))
        {
            correlationId = correlationIdHeader.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdConstants.HeaderName] = correlationId;
        }

        context.Items[correlationIdKey] = correlationId;
        
        // Add correlation ID to response
        context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { { "CorrelationId", correlationId } }))
        {
            _logger.LogDebug("Request started with correlation ID: {CorrelationId}", correlationId);
            await _next(context);
            _logger.LogDebug("Request completed with correlation ID: {CorrelationId}", correlationId);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            return correlationId?.ToString();
        }
        return null;
    }
}
