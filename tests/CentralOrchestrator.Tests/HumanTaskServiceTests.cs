using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralOrchestrator.Tests;

public class HumanTaskServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentRegistryDbContext _dbContext;
    private readonly HumanTaskService _taskService;

    public HumanTaskServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentRegistryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AgentRegistryDbContext(options);
        _dbContext.Database.EnsureCreated();

        _taskService = new HumanTaskService(_dbContext, NullLogger<HumanTaskService>.Instance);
    }

    [Fact]
    public async Task CreateTaskAsync_AddsTaskToDatabase()
    {
        var request = new CreateHumanTaskRequest(
            EventId: "evt_1001",
            Title: "Approve Q3 Sales Analysis",
            Description: "Please review the SQL query results before emailing management",
            AssignedAgentId: "sql-data-agent",
            Priority: "High"
        );

        var task = await _taskService.CreateTaskAsync(request);

        Assert.NotNull(task);
        Assert.True(task.Id > 0);
        Assert.Equal("Approve Q3 Sales Analysis", task.Title);
        Assert.Equal("PendingHumanAction", task.Status);
        Assert.Equal("High", task.Priority);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_UpdatesStatusAndCompletedTimestamp()
    {
        var task = await _taskService.CreateTaskAsync(new CreateHumanTaskRequest(
            EventId: "evt_1002",
            Title: "Manual Review Required",
            Description: "Review document intelligence extracted fields",
            AssignedAgentId: null,
            Priority: "Medium"
        ));

        var updated = await _taskService.UpdateTaskStatusAsync(task.Id, new UpdateTaskStatusRequest("Completed"));
        Assert.True(updated);

        var retrieved = await _taskService.GetTaskByIdAsync(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Completed", retrieved.Status);
        Assert.NotNull(retrieved.CompletedAt);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
