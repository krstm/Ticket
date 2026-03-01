using Serilog;
using Ticket.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddProblemDetails();
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.ConfigureRequestPipeline();

app.Run();

public partial class Program;
