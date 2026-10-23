using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        double SlaValue(string key, double fallback) =>
            double.TryParse(configuration[key], out var value) ? value : fallback;

        var sla = new SlaSettings(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = SlaValue("Sla:BaseWindowHours:Low", 72),
                [TicketPriority.Medium] = SlaValue("Sla:BaseWindowHours:Medium", 24),
                [TicketPriority.High] = SlaValue("Sla:BaseWindowHours:High", 8),
                [TicketPriority.Critical] = SlaValue("Sla:BaseWindowHours:Critical", 4)
            },
            SlaValue("Sla:PremiumCustomerMultiplier", 0.5),
            SlaValue("Sla:MinimumWindowHours", 1),
            SlaValue("Sla:AtRiskThresholdPercent", 25));
        services.AddSingleton(sla);
        services.AddSingleton<SlaPolicy>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}
