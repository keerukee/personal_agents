using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CentralOrchestrator.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events");

        group.MapPost("/inbound", async (
            [FromBody] AgentEventMessage eventMessage,
            [FromServices] ITaskPlanner taskPlanner,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Received Generic Inbound Event {EventId} from {Source}", eventMessage.EventId, eventMessage.Source);

            var plan = await taskPlanner.CreatePlanAsync(eventMessage, cancellationToken);
            var executedPlan = await taskPlanner.ExecutePlanAsync(plan, cancellationToken);

            return Results.Ok(new
            {
                message = "Generic agent event processed successfully",
                eventId = eventMessage.EventId,
                planId = executedPlan.PlanId,
                status = executedPlan.Status,
                stepCount = executedPlan.Steps.Count,
                steps = executedPlan.Steps
            });
        });
    }
}
