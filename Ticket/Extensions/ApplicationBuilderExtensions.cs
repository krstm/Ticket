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
        app.UseStaticFiles();

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
