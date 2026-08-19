using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CentralOrchestrator.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tasks");

        group.MapGet("/", async ([FromQuery] string? status, [FromServices] IHumanTaskService taskService) =>
        {
            var tasks = await taskService.GetTasksAsync(status);
            return Results.Ok(tasks);
        });

        group.MapGet("/{id:int}", async (int id, [FromServices] IHumanTaskService taskService) =>
        {
            var task = await taskService.GetTaskByIdAsync(id);
            return task != null ? Results.Ok(task) : Results.NotFound(new { error = $"Task #{id} not found" });
        });

        group.MapPost("/", async ([FromBody] CreateHumanTaskRequest request, [FromServices] IHumanTaskService taskService) =>
        {
            var task = await taskService.CreateTaskAsync(request);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapPut("/{id:int}/status", async (int id, [FromBody] UpdateTaskStatusRequest request, [FromServices] IHumanTaskService taskService) =>
        {
            var success = await taskService.UpdateTaskStatusAsync(id, request);
            return success ? Results.Ok(new { message = $"Task #{id} status updated to {request.Status}" }) : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, [FromServices] IHumanTaskService taskService) =>
        {
            var success = await taskService.DeleteTaskAsync(id);
            return success ? Results.Ok(new { message = $"Task #{id} deleted" }) : Results.NotFound();
        });
    }
}
