using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticket.Configuration;
using Ticket.Data;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;

namespace Ticket.Tests.TestUtilities;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TicketTests-{Guid.NewGuid()}";
    private readonly int _rateLimit;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly FakeClock _clock = new();

    public CustomWebApplicationFactory() : this(1000)
    {
    }

    private CustomWebApplicationFactory(int rateLimit, Action<IServiceCollection>? configureServices = null)
    {
        _rateLimit = rateLimit;
        _configureServices = configureServices;
    }

    public static CustomWebApplicationFactory Create(int rateLimit, Action<IServiceCollection>? configure = null) =>
        new(rateLimit, configure);

    public FakeClock Clock => _clock;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            var overrides = new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:PermitLimit"] = _rateLimit.ToString(),
                [$"{RateLimitingOptions.SectionName}:WindowSeconds"] = "1",
                [$"{RateLimitingOptions.SectionName}:QueueLimit"] = "0"
            };
            config.AddInMemoryCollection(overrides!);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            var notificationDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(INotificationService));
            if (notificationDescriptor != null)
            {
                services.Remove(notificationDescriptor);
            }

            services.AddSingleton<TestNotificationService>();
            services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<TestNotificationService>());
            services.AddSingleton<IClock>(_ => _clock);
            services.AddSingleton(_clock);

            _configureServices?.Invoke(services);

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public async Task ResetStateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var notifications = scope.ServiceProvider.GetRequiredService<TestNotificationService>();
        notifications.Reset();
        _clock.Set(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
    }
}
