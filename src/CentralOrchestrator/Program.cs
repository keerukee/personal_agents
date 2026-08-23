using CentralOrchestrator.Data;
using CentralOrchestrator.Endpoints;
using CentralOrchestrator.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQL Server / SQLite DbContext for Dynamic Agent Registry & Database Queue
var connectionString = builder.Configuration.GetConnectionString("AgentRegistryConnection") 
    ?? "Server=localhost;Database=AgentRegistryDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AgentRegistryDbContext>(options =>
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

// Register Services
builder.Services.AddScoped<IAgentRegistryService, AgentRegistryService>();
builder.Services.AddScoped<IHumanTaskService, HumanTaskService>();
builder.Services.AddScoped<IDatabaseQueueService, DatabaseQueueService>();
builder.Services.AddHttpClient<IMcpHttpClient, StreamableMcpHttpClient>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITaskPlanner, TaskPlannerService>();

// Register Central Orchestrator Database Queue Worker
builder.Services.AddHostedService<OrchestratorQueueWorker>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Ensure Database is created and all required tables exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentRegistryDbContext>();
    dbContext.Database.EnsureCreated();

    // Ensure HumanTasks, InboundEvents, and AgentTasks tables exist in existing database
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
                    [DataJson] NVARCHAR(MAX) NOT NULL DEFAULT '{}',
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
                    [PayloadJson] NVARCHAR(MAX) NOT NULL DEFAULT '{}',
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

app.MapGet("/", () => Results.Ok(new {
    service = "Central Orchestrator Agent (.NET 10 LTS)",
    architecture = "100% Disconnected Database-Driven Event Bus & Task Queue",
    database = "Microsoft SQL Server (AgentRegistryDb)",
    status = "Online",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapEventEndpoints();
app.MapRegistryEndpoints();
app.MapTaskEndpoints();

app.Run();
