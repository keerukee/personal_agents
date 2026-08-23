using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralOrchestrator.Tests;

public class DatabaseQueueServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentRegistryDbContext _dbContext;
    private readonly DatabaseQueueService _queueService;

    public DatabaseQueueServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentRegistryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AgentRegistryDbContext(options);
        _dbContext.Database.EnsureCreated();

        _queueService = new DatabaseQueueService(_dbContext, NullLogger<DatabaseQueueService>.Instance);
    }

    [Fact]
    public async Task CreateInboundEventAsync_InsertsPendingRecord()
    {
        var request = new CreateInboundEventRequest(
            Source: "OutlookEmailAgent",
            Prompt: "Run regional sales analysis for July 2026",
            DataJson: "{\"subject\": \"Run Sales Query\"}"
        );

        var eventDto = await _queueService.CreateInboundEventAsync(request);

        Assert.NotNull(eventDto);
        Assert.NotEmpty(eventDto.EventGuid);
        Assert.Equal("Pending", eventDto.Status);
        Assert.Equal("OutlookEmailAgent", eventDto.Source);

        var pending = await _queueService.GetPendingInboundEventsAsync();
        Assert.Single(pending);
        Assert.Equal(eventDto.EventGuid, pending.First().EventGuid);
    }

    [Fact]
    public async Task CreateAgentTasksAsync_EnqueuesChildTasks()
    {
        var evt = await _queueService.CreateInboundEventAsync(new CreateInboundEventRequest(
            Source: "BlazorDashboardUser",
            Prompt: "Execute script and query DB"
        ));

        var taskRequests = new List<CreateAgentTaskRequest>
        {
            new(evt.EventGuid, 1, "sql-data-agent", "query_database", "{\"sql\":\"SELECT 1\"}"),
            new(evt.EventGuid, 2, "python-runner-agent", "execute_script", "{\"code\":\"print(123)\"}")
        };

        var tasks = await _queueService.CreateAgentTasksAsync(evt.EventGuid, taskRequests);

        Assert.Equal(2, tasks.Count);
        Assert.Equal("Pending", tasks[0].Status);
        Assert.Equal("PendingDependency", tasks[1].Status);

        var pendingSql = await _queueService.GetPendingTasksForAgentAsync("sql-data-agent");
        Assert.Single(pendingSql);
        Assert.Equal("query_database", pendingSql.First().Action);
    }

    [Fact]
    public async Task ClaimTaskAsync_And_UpdateTaskResultAsync_ProgressesStatus()
    {
        var evt = await _queueService.CreateInboundEventAsync(new CreateInboundEventRequest(
            Source: "Test", Prompt: "Test Claim"
        ));

        var tasks = await _queueService.CreateAgentTasksAsync(evt.EventGuid, new List<CreateAgentTaskRequest>
        {
            new(evt.EventGuid, 1, "sql-data-agent", "query_database", "{}")
        });

        var taskGuid = tasks.First().TaskGuid;

        var claimed = await _queueService.ClaimTaskAsync(taskGuid);
        Assert.True(claimed);

        var updated = await _queueService.UpdateTaskResultAsync(taskGuid, new UpdateTaskResultRequest(
            Status: "Completed",
            ResultJson: "{\"output\":\"Query completed successfully\"}"
        ));

        Assert.True(updated);

        var allTasks = await _queueService.GetTasksForEventAsync(evt.EventGuid);
        Assert.Equal("Completed", allTasks.First().Status);
        Assert.Contains("Query completed", allTasks.First().ResultJson);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
