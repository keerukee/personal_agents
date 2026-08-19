using CentralOrchestrator.Data;
using CentralOrchestrator.Endpoints;
using CentralOrchestrator.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQL Server / SQLite DbContext for Dynamic Agent Registry & Human Tasks
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
builder.Services.AddHttpClient<IMcpHttpClient, StreamableMcpHttpClient>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITaskPlanner, TaskPlannerService>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Ensure Database is created and all required tables exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentRegistryDbContext>();
    dbContext.Database.EnsureCreated();

    // Ensure HumanTasks table exists in existing database
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
        ");
    }

    var registry = scope.ServiceProvider.GetRequiredService<IAgentRegistryService>();
    await registry.SeedDefaultAgentsAsync();
}

app.MapGet("/", () => Results.Ok(new {
    service = "Central Orchestrator Agent (.NET 10 LTS)",
    status = "Online",
    database = "Microsoft SQL Server (AgentRegistryDb)",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapEventEndpoints();
app.MapRegistryEndpoints();
app.MapTaskEndpoints();

app.Run();
