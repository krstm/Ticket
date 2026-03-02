using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticket.Configuration;
using Ticket.Data;
using Ticket.Filters;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;
using Ticket.ModelBinding;
using Ticket.Services;
using Ticket.Services.Infrastructure;
using Ticket.Validators;

namespace Ticket.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var provider = configuration.GetValue<string>("Database:Provider");
            if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryDatabase("TicketDb");
            }
            else
            {
                options.UseSqlServer(configuration.GetConnectionString("TicketDb"));
            }
        });

        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<INotificationService, NullNotificationService>();
        services.AddScoped<IDepartmentService, DepartmentService>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IContentSanitizer, HtmlContentSanitizer>();
        services.AddSingleton<TicketAccessEvaluator>();

        services.AddMediatR(typeof(Program).Assembly);

        services.AddFluentValidationAutoValidation()
            .AddValidatorsFromAssemblyContaining<TicketCreateRequestValidator>();

        services.AddControllersWithViews(options =>
        {
            options.Filters.Add<ValidateModelFilter>();
            options.ModelBinderProviders.Insert(0, new TrimmingModelBinderProvider());
        });

        services.AddRateLimiter(options =>
        {
            var limiterOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limiterOptions.PermitLimit,
                    Window = TimeSpan.FromSeconds(Math.Max(1, limiterOptions.WindowSeconds)),
                    QueueLimit = Math.Max(0, limiterOptions.QueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("mutations", opt =>
            {
                opt.PermitLimit = Math.Max(1, limiterOptions.PermitLimit / 2);
                opt.Window = TimeSpan.FromSeconds(Math.Max(1, limiterOptions.WindowSeconds));
                opt.QueueLimit = Math.Max(0, limiterOptions.QueueLimit);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        return services;
    }
}
