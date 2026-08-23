using Common.Contracts;
using System.Text.Json;

namespace CentralOrchestrator.Services;

public class OrchestratorQueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchestratorQueueWorker> _logger;

    public OrchestratorQueueWorker(IServiceProvider serviceProvider, ILogger<OrchestratorQueueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Central Orchestrator Database Queue Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<IDatabaseQueueService>();
                var taskPlanner = scope.ServiceProvider.GetRequiredService<ITaskPlanner>();

                // 1. Fetch pending inbound events from SQL Server
                var pendingEvents = await queueService.GetPendingInboundEventsAsync(limit: 5);

                foreach (var evt in pendingEvents)
                {
                    _logger.LogInformation("Processing Pending Event '{EventGuid}' from Source '{Source}'", evt.EventGuid, evt.Source);
                    await queueService.UpdateInboundEventStatusAsync(evt.EventGuid, "Processing");

                    // 2. Parse event payload into AgentEventMessage
                    JsonElement dataObj;
                    try
                    {
                        dataObj = JsonDocument.Parse(evt.DataJson).RootElement;
                    }
                    catch
                    {
                        dataObj = JsonDocument.Parse("{}").RootElement;
                    }

                    var eventMsg = new AgentEventMessage(
                        EventId: evt.EventGuid,
                        Source: evt.Source,
                        Timestamp: evt.CreatedAt,
                        Prompt: evt.Prompt,
                        Data: dataObj
                    );

                    // 3. Use LLM TaskPlanner to build DAG execution steps
                    var plan = await taskPlanner.CreatePlanAsync(eventMsg, stoppingToken);

                    // 4. Enqueue planned steps into AgentTasks table in SQL Server
                    var taskRequests = plan.Steps.Select(step => new CreateAgentTaskRequest(
                        ParentEventGuid: evt.EventGuid,
                        StepOrder: step.StepId,
                        TargetAgentId: step.AgentId,
                        Action: step.Action,
                        PayloadJson: JsonSerializer.Serialize(step.Parameters)
                    )).ToList();

                    if (taskRequests.Count > 0)
                    {
                        await queueService.CreateAgentTasksAsync(evt.EventGuid, taskRequests);
                        _logger.LogInformation("Enqueued {Count} sub-agent tasks in DB for Event '{EventGuid}'", taskRequests.Count, evt.EventGuid);
                    }
                    else
                    {
                        _logger.LogInformation("No matching sub-agent tasks generated for Event '{EventGuid}'", evt.EventGuid);
                        await queueService.UpdateInboundEventStatusAsync(evt.EventGuid, "Completed");
                    }
                }

                // 5. Monitor and conclude events whose child AgentTasks have all finished
                await CheckAndCompleteProcessedEventsAsync(queueService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in OrchestratorQueueWorker execution loop");
            }

            // Polling interval (2 seconds)
            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task CheckAndCompleteProcessedEventsAsync(IDatabaseQueueService queueService)
    {
        var processingEvents = (await queueService.GetAllInboundEventsAsync(50))
            .Where(e => e.Status == "Processing")
            .ToList();

        foreach (var evt in processingEvents)
        {
            var tasks = await queueService.GetTasksForEventAsync(evt.EventGuid);
            if (tasks.Count > 0 && tasks.All(t => t.Status == "Completed" || t.Status == "Failed" || t.Status == "Skipped"))
            {
                var finalStatus = tasks.Any(t => t.Status == "Failed") ? "CompletedWithErrors" : "Completed";
                await queueService.UpdateInboundEventStatusAsync(evt.EventGuid, finalStatus);
                _logger.LogInformation("Concluded Inbound Event '{EventGuid}' with Status '{Status}'", evt.EventGuid, finalStatus);
            }
        }
    }
}
