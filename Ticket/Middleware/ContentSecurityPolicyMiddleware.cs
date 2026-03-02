using Microsoft.AspNetCore.Http;

namespace Ticket.Middleware;

public sealed class ContentSecurityPolicyMiddleware
{
    private readonly RequestDelegate _next;

    public ContentSecurityPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // 1. CSP (Content Security Policy)
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers.Append("Content-Security-Policy", 
                    "default-src 'self'; " +
                    "script-src 'self'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "font-src 'self'; " +
                    "img-src 'self' data:; " +
                    "connect-src 'self'; " +
                    "frame-ancestors 'none'; " +
                    "base-uri 'self'; " +
                    "form-action 'self'; " +
                    "object-src 'none'");
            }

            // 2. Clickjacking & MIME Sniffing
            headers.Append("X-Frame-Options", "SAMEORIGIN");
            headers.Append("X-Content-Type-Options", "nosniff");

            // 3. Privacy & Feature Isolation
            headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");

            // 4. Cross-Origin Policies (COOP/COEP/CORP)
            headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
            headers.Append("Cross-Origin-Opener-Policy", "same-origin");
            headers.Append("Cross-Origin-Resource-Policy", "same-origin");

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
