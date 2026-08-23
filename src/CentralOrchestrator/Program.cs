using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using CentralOrchestrator.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // Register Home (Google Gemini) & Office (Azure AI Foundry) Providers
        services.AddHttpClient<GoogleGeminiLlmProvider>();
        services.AddHttpClient<GoogleDocumentAiProvider>();
        services.AddHttpClient<AzureAiFoundryLlmProvider>();
        services.AddHttpClient<AzureDocumentIntelligenceProvider>();
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();

        // Core Orchestration Services
        services.AddScoped<IAgentRegistryService, AgentRegistryService>();
        services.AddScoped<IHumanTaskService, HumanTaskService>();
        services.AddScoped<IDatabaseQueueService, DatabaseQueueService>();
        services.AddHttpClient<IMcpHttpClient, StreamableMcpHttpClient>();
        services.AddHttpClient();
        services.AddScoped<ITaskPlanner, TaskPlannerService>();

        // Pure Database Queue Worker Service (Zero HTTP / WebAPI)
        services.AddHostedService<OrchestratorQueueWorker>();
    })
    .Build();

// Ensure Database & all required SQL Server tables exist on startup
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentRegistryDbContext>();
    var aiFactory = scope.ServiceProvider.GetRequiredService<IAiProviderFactory>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Central Orchestrator starting up in 100% Pure Disconnected Worker Mode (Zero WebAPI)...");
    logger.LogInformation("Active AI Environment Mode: '{Environment}' (LLM: {LlmProvider}, DocIntel: {DocProvider})",
        aiFactory.ActiveEnvironment,
        aiFactory.GetLlmProvider().ProviderName,
        aiFactory.GetDocumentIntelligenceProvider().ProviderName);

    dbContext.Database.EnsureCreated();

    if (dbContext.Database.IsSqlServer())
    {
        dbContext.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HumanTasks')
            BEGIN
                CREATE TABLE [HumanTasks] (
                    [Id] int NOT NULL IDENTITY,
                    [EventId] nvarchar(450) NOT NULL,
                    [Title] nvarchar(max) NOT NULL,
                    [Description] nvarchar(max) NOT NULL,
                    [AssignedAgentId] nvarchar(450) NULL,
                    [Status] nvarchar(100) NOT NULL DEFAULT 'PendingHumanAction',
                    [Priority] nvarchar(50) NOT NULL DEFAULT 'Medium',
                    [CreatedAt] datetimeoffset NOT NULL,
                    [CompletedAt] datetimeoffset NULL,
                    CONSTRAINT [PK_HumanTasks] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InboundEvents')
            BEGIN
                CREATE TABLE [InboundEvents] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [EventGuid] NVARCHAR(450) NOT NULL UNIQUE,
                    [Source] NVARCHAR(100) NOT NULL,
                    [Prompt] NVARCHAR(MAX) NOT NULL,
                    [DataJson] NVARCHAR(MAX) NOT NULL DEFAULT '{{}}',
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    [CreatedAt] DATETIMEOFFSET NOT NULL,
                    [ProcessedAt] DATETIMEOFFSET NULL
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentTasks')
            BEGIN
                CREATE TABLE [AgentTasks] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [TaskGuid] NVARCHAR(450) NOT NULL UNIQUE,
                    [ParentEventGuid] NVARCHAR(450) NOT NULL,
                    [StepOrder] INT NOT NULL DEFAULT 1,
                    [TargetAgentId] NVARCHAR(450) NOT NULL,
                    [Action] NVARCHAR(100) NOT NULL,
                    [PayloadJson] NVARCHAR(MAX) NOT NULL DEFAULT '{{}}',
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    [ResultJson] NVARCHAR(MAX) NULL,
                    [ErrorMessage] NVARCHAR(MAX) NULL,
                    [CreatedAt] DATETIMEOFFSET NOT NULL,
                    [StartedAt] DATETIMEOFFSET NULL,
                    [CompletedAt] DATETIMEOFFSET NULL,
                    CONSTRAINT [FK_AgentTasks_InboundEvents] FOREIGN KEY ([ParentEventGuid]) REFERENCES [InboundEvents] ([EventGuid]) ON DELETE CASCADE
                );
            END
        ");
    }

    var registry = scope.ServiceProvider.GetRequiredService<IAgentRegistryService>();
    await registry.SeedDefaultAgentsAsync();
}

await host.RunAsync();
