using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralOrchestrator.Tests;

public class AgentRegistryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentRegistryDbContext _dbContext;
    private readonly AgentRegistryService _service;

    public AgentRegistryServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentRegistryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AgentRegistryDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new AgentRegistryService(_dbContext, NullLogger<AgentRegistryService>.Instance);
    }

    [Fact]
    public async Task SeedDefaultAgentsAsync_PopulatesDatabase()
    {
        await _service.SeedDefaultAgentsAsync();

        var agents = await _service.GetAllActiveAgentsAsync();

        Assert.NotEmpty(agents);
        Assert.Contains(agents, a => a.Id == "sql-data-agent");
        Assert.Contains(agents, a => a.Id == "python-runner-agent");
        Assert.Contains(agents, a => a.Id == "outlook-email-agent");
    }

    [Fact]
    public async Task RegisterOrUpdateAgent_CreatesAndUpdatesAgent()
    {
        var request = new RegisterAgentRequest(
            Id: "test-agent-1",
            Name: "TestAgent",
            Description: "A test agent",
            EndpointUrl: "http://localhost:9999/mcp",
            TransportType: "StreamableHttpMcp",
            Capabilities: new List<AgentCapabilityDto>
            {
                new("test_tool", "Execute test tool", "{}")
            }
        );

        var registered = await _service.RegisterOrUpdateAgentAsync(request);

        Assert.NotNull(registered);
        Assert.Equal("test-agent-1", registered.Id);
        Assert.True(registered.IsActive);
        Assert.Single(registered.Capabilities);

        // Update agent name and capability
        var updateRequest = request with
        {
            Name = "UpdatedTestAgent",
            Capabilities = new List<AgentCapabilityDto>
            {
                new("test_tool_v2", "Execute updated test tool", "{}")
            }
        };

        var updated = await _service.RegisterOrUpdateAgentAsync(updateRequest);

        Assert.Equal("UpdatedTestAgent", updated.Name);
        Assert.Equal("test_tool_v2", updated.Capabilities.First().CapabilityName);
    }

    [Fact]
    public async Task GetAgentsByCapability_ReturnsMatchingAgents()
    {
        await _service.SeedDefaultAgentsAsync();

        var sqlAgents = await _service.GetAgentsByCapabilityAsync("query_database");

        Assert.Single(sqlAgents);
        Assert.Equal("sql-data-agent", sqlAgents.First().Id);
    }

    [Fact]
    public async Task DeactivateAgent_MarksAsInactive()
    {
        await _service.SeedDefaultAgentsAsync();

        var success = await _service.DeactivateAgentAsync("sql-data-agent");
        Assert.True(success);

        var agent = await _service.GetAgentByIdAsync("sql-data-agent");
        Assert.NotNull(agent);
        Assert.False(agent.IsActive);

        var activeAgents = await _service.GetAllActiveAgentsAsync();
        Assert.DoesNotContain(activeAgents, a => a.Id == "sql-data-agent");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
