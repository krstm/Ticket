using Microsoft.AspNetCore.Builder;
using Serilog;
using Ticket.Middleware;

namespace Ticket.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication ConfigureRequestPipeline(this WebApplication app)
    {
        var isPlaywrightEnv = app.Environment.IsEnvironment("Playwright");

        if (!app.Environment.IsDevelopment() && !isPlaywrightEnv)
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<ContentSecurityPolicyMiddleware>();

        if (!isPlaywrightEnv)
        {
            app.UseHttpsRedirection();
        }
        
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                ctx.Context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
                ctx.Context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
                ctx.Context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");
                ctx.Context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
                ctx.Context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
                ctx.Context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
            }
        });

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
