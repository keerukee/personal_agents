using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using CentralOrchestrator.Services.AI;
using MySqlDataAgent;
using Microsoft.Extensions.DependencyInjection;

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

        // Register LLM Providers for Agent-Local AI
        services.AddHttpClient<GoogleGeminiLlmProvider>();
        services.AddHttpClient<AzureAiFoundryLlmProvider>();
        services.AddHttpClient<GoogleDocumentAiProvider>();
        services.AddHttpClient<AzureDocumentIntelligenceProvider>();
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();

        services.AddScoped<IDatabaseQueueService, DatabaseQueueService>();
        services.AddHostedService<MySqlQueueWorker>();
    })
    .Build();

await host.RunAsync();
