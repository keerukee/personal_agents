using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace CentralOrchestrator.Tests;

public class TaskPlannerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentRegistryDbContext _dbContext;
    private readonly AgentRegistryService _registryService;
    private readonly TaskPlannerService _plannerService;

    public TaskPlannerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentRegistryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AgentRegistryDbContext(options);
        _dbContext.Database.EnsureCreated();

        _registryService = new AgentRegistryService(_dbContext, NullLogger<AgentRegistryService>.Instance);
        _registryService.SeedDefaultAgentsAsync().GetAwaiter().GetResult();

        var httpClient = new HttpClient();
        var mcpClient = new StreamableMcpHttpClient(httpClient, NullLogger<StreamableMcpHttpClient>.Instance);

        _plannerService = new TaskPlannerService(
            _registryService,
            mcpClient,
            httpClient,
            NullLogger<TaskPlannerService>.Instance
        );
    }

    [Fact]
    public async Task CreatePlanAsync_BuildsGenericStepsFromInboundEvent()
    {
        var rawJsonData = JsonSerializer.SerializeToElement(new
        {
            subject = "Run Sales Database Query",
            entryId = "0000000038A611D0B14800A0C922E82000000000"
        });

        var eventMessage = new AgentEventMessage(
            EventId: "evt_generic_1001",
            Source: "OutlookEmailAgent",
            Timestamp: DateTimeOffset.UtcNow,
            Prompt: "Please run database query for July 2026 sales report",
            Data: rawJsonData
        );

        var plan = await _plannerService.CreatePlanAsync(eventMessage);

        Assert.NotNull(plan);
        Assert.Equal("evt_generic_1001", plan.EventId);
        Assert.NotEmpty(plan.Steps);

        // Verify that SqlDataAgent was dynamically matched based on capability description
        Assert.Contains(plan.Steps, s => s.AgentId == "sql-data-agent");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
