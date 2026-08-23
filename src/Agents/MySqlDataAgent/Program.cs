using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlDataAgent;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var connectionString = hostContext.Configuration.GetConnectionString("AgentRegistryConnection")
            ?? "Server=localhost;Database=AgentRegistryDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AgentRegistryDbContext>(options =>
        {
            if (connectionString.Contains("Data Source=") && connectionString.EndsWith(".db"))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IDatabaseQueueService, DatabaseQueueService>();
        services.AddHostedService<MySqlQueueWorker>();
    })
    .Build();

await host.RunAsync();
