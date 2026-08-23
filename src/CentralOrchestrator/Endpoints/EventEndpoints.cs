using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CentralOrchestrator.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events");

        // Enqueue new inbound event into SQL Server queue
        group.MapPost("/enqueue", async (
            [FromBody] CreateInboundEventRequest request,
            [FromServices] IDatabaseQueueService queueService) =>
        {
            var eventDto = await queueService.CreateInboundEventAsync(request);
            return Results.Created($"/api/events/queue/{eventDto.EventGuid}", eventDto);
        });

        // List inbound events from SQL Server
        group.MapGet("/queue", async ([FromQuery] int limit, [FromServices] IDatabaseQueueService queueService) =>
        {
            var events = await queueService.GetAllInboundEventsAsync(limit > 0 ? limit : 50);
            return Results.Ok(events);
        });

        // List child tasks for specific event
        group.MapGet("/queue/{eventGuid}/tasks", async (string eventGuid, [FromServices] IDatabaseQueueService queueService) =>
        {
            var tasks = await queueService.GetTasksForEventAsync(eventGuid);
            return Results.Ok(tasks);
        });

        // Map Sub-Agent DB Queue Workers API
        var agentTaskGroup = routes.MapGroup("/api/agent-tasks");

        // Sub-agent polls pending tasks
        agentTaskGroup.MapGet("/pending/{agentId}", async (string agentId, [FromServices] IDatabaseQueueService queueService) =>
        {
            var tasks = await queueService.GetPendingTasksForAgentAsync(agentId);
            return Results.Ok(tasks);
        });

        // Sub-agent claims task
        agentTaskGroup.MapPost("/{taskGuid}/claim", async (string taskGuid, [FromServices] IDatabaseQueueService queueService) =>
        {
            var success = await queueService.ClaimTaskAsync(taskGuid);
            return success ? Results.Ok(new { message = "Task claimed successfully" }) : Results.BadRequest(new { error = "Task unavailable or already claimed" });
        });

        // Sub-agent posts task result
        agentTaskGroup.MapPost("/{taskGuid}/complete", async (string taskGuid, [FromBody] UpdateTaskResultRequest request, [FromServices] IDatabaseQueueService queueService) =>
        {
            var success = await queueService.UpdateTaskResultAsync(taskGuid, request);
            return success ? Results.Ok(new { message = "Task result updated" }) : Results.NotFound();
        });
    }
}
