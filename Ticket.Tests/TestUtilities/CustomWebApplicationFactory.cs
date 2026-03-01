using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticket.Configuration;
using Ticket.Data;

namespace Ticket.Tests.TestUtilities;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TicketTests-{Guid.NewGuid()}";
    private readonly int _rateLimit;

    public CustomWebApplicationFactory() : this(1000)
    {
    }

    private CustomWebApplicationFactory(int rateLimit)
    {
        _rateLimit = rateLimit;
    }

    public static CustomWebApplicationFactory Create(int rateLimit) => new(rateLimit);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            var overrides = new Dictionary<string, string?>
            {
                [$"{ApiKeyOptions.SectionName}:CategoryManagement"] = "integration-key",
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
    }
}
