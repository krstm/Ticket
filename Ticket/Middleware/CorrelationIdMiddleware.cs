using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ticket.Middleware;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;
        }

        context.TraceIdentifier = correlationId!;
        context.Response.Headers[HeaderName] = correlationId!;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId! }))
        {
            await _next(context);
        }
    }
}
