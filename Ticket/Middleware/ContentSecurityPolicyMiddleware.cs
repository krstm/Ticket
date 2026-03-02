using Microsoft.AspNetCore.Http;

namespace Ticket.Middleware;

public sealed class ContentSecurityPolicyMiddleware
{
    private const string HeaderName = "Content-Security-Policy";
    private readonly RequestDelegate _next;
    private readonly string _policy;

    public ContentSecurityPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
        _policy = string.Join(' ',
            "default-src 'self';",
            "script-src 'self';",
            "style-src 'self' 'unsafe-inline';",
            "font-src 'self';",
            "img-src 'self' data:;",
            "connect-src 'self';",
            "frame-ancestors 'none';",
            "base-uri 'self';",
            "form-action 'self';",
            "object-src 'none'");
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers.Append(HeaderName, _policy);
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
