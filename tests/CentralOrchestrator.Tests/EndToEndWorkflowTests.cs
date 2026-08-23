using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using CentralOrchestrator.Services.AI;
using Common.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralOrchestrator.Tests;

public class EndToEndWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentRegistryDbContext _dbContext;
    private readonly DatabaseQueueService _queueService;
    private readonly AgentRegistryService _registryService;
    private readonly TaskPlannerService _plannerService;

    public EndToEndWorkflowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AgentRegistryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AgentRegistryDbContext(options);
        _dbContext.Database.EnsureCreated();

        _queueService = new DatabaseQueueService(_dbContext, NullLogger<DatabaseQueueService>.Instance);
        _registryService = new AgentRegistryService(_dbContext, NullLogger<AgentRegistryService>.Instance);

        var inMemoryConfig = new Dictionary<string, string?> { { "AiSettings:Environment", "Home" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient<GoogleGeminiLlmProvider>();
        services.AddHttpClient<GoogleDocumentAiProvider>();
        services.AddHttpClient<AzureAiFoundryLlmProvider>();
        services.AddHttpClient<AzureDocumentIntelligenceProvider>();
        services.AddSingleton(NullLogger<AiProviderFactory>.Instance);
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
        var provider = services.BuildServiceProvider();

        var httpClient = provider.GetRequiredService<HttpClient>();
        var mcpClient = new StreamableMcpHttpClient(httpClient, NullLogger<StreamableMcpHttpClient>.Instance);

        _plannerService = new TaskPlannerService(
            _registryService,
            mcpClient,
            httpClient,
            NullLogger<TaskPlannerService>.Instance
        );
    }

    [Fact]
    public async Task EndToEnd_PatientReportEmail_WorkflowTest()
    {
        // 1. Seed agents into registry DB
        await _registryService.SeedDefaultAgentsAsync();

        // 2. Simulate Outlook Agent enqueuing email from Gmail to keerukee@outlook.com
        var eventRequest = new CreateInboundEventRequest(
            Source: "OutlookEmailAgent",
            Prompt: "Incoming email from gmail_sender@gmail.com to keerukee@outlook.com: Please provide last 5 patients information AI response.",
            DataJson: "{\"sender\": \"gmail_sender@gmail.com\", \"recipient\": \"keerukee@outlook.com\", \"subject\": \"Request: Last 5 Patients Information\"}"
        );

        var inboundEvent = await _queueService.CreateInboundEventAsync(eventRequest);
        Assert.NotNull(inboundEvent);
        Assert.Equal("Pending", inboundEvent.Status);

        // 3. Central Orchestrator processes event and plans sub-agent tasks
        var eventMsg = new AgentEventMessage(
            EventId: inboundEvent.EventGuid,
            Source: inboundEvent.Source,
            Timestamp: inboundEvent.CreatedAt,
            Prompt: inboundEvent.Prompt,
            Data: System.Text.Json.JsonDocument.Parse(inboundEvent.DataJson).RootElement
        );

        var plan = await _plannerService.CreatePlanAsync(eventMsg);
        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Steps);

        var taskRequests = plan.Steps.Select(s => new CreateAgentTaskRequest(
            ParentEventGuid: inboundEvent.EventGuid,
            StepOrder: s.StepId,
            TargetAgentId: s.AgentId,
            Action: s.Action,
            PayloadJson: System.Text.Json.JsonSerializer.Serialize(s.Parameters)
        )).ToList();

        var createdTasks = await _queueService.CreateAgentTasksAsync(inboundEvent.EventGuid, taskRequests);
        Assert.NotEmpty(createdTasks);

        // 4. Verify MySqlDataAgent claims and executes patient query task
        var pendingMySqlTasks = await _queueService.GetPendingTasksForAgentAsync("mysql-data-agent");
        Assert.NotEmpty(pendingMySqlTasks);
        var mysqlTask = pendingMySqlTasks.First();

        var claimed = await _queueService.ClaimTaskAsync(mysqlTask.TaskGuid);
        Assert.True(claimed);

        var mysqlResultMarkdown = @"### 🏥 MySQL Lab Reports & Patients Information Report
| Patient ID | Patient Name | Age | Gender | Test / Lab Report | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| P-1005 | John Doe | 42 | Male | Complete Blood Count (CBC) | Normal |
| P-1004 | Jane Smith | 38 | Female | Lipid Panel | Cholesterol: 195 mg/dL |
| P-1003 | Robert Johnson | 55 | Male | HbA1c Diabetes Screen | 5.8% (Pre-diabetic) |
| P-1002 | Emily Davis | 29 | Female | Thyroid Panel (TSH) | 2.1 mIU/L |
| P-1001 | Michael Brown | 61 | Male | Comprehensive Metabolic | Normal |";

        var updated = await _queueService.UpdateTaskResultAsync(mysqlTask.TaskGuid, new UpdateTaskResultRequest(
            Status: "Completed",
            ResultJson: System.Text.Json.JsonSerializer.Serialize(new { output = mysqlResultMarkdown })
        ));
        Assert.True(updated);

        // 5. Verify task completion and event status update
        var eventTasks = await _queueService.GetTasksForEventAsync(inboundEvent.EventGuid);
        Assert.Contains(eventTasks, t => t.TargetAgentId == "mysql-data-agent" && t.Status == "Completed");

        await _queueService.UpdateInboundEventStatusAsync(inboundEvent.EventGuid, "Completed");
        var finalEvents = await _queueService.GetAllInboundEventsAsync();
        Assert.Equal("Completed", finalEvents.First(e => e.EventGuid == inboundEvent.EventGuid).Status);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
