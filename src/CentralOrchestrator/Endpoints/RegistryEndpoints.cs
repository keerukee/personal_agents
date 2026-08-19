using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CentralOrchestrator.Endpoints;

public static class RegistryEndpoints
{
    public static void MapRegistryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/registry");

        group.MapGet("/agents", async ([FromServices] IAgentRegistryService registry) =>
        {
            var agents = await registry.GetAllActiveAgentsAsync();
            return Results.Ok(agents);
        });

        group.MapGet("/agents/{id}", async (string id, [FromServices] IAgentRegistryService registry) =>
        {
            var agent = await registry.GetAgentByIdAsync(id);
            return agent != null ? Results.Ok(agent) : Results.NotFound(new { error = $"Agent '{id}' not found" });
        });

        group.MapGet("/agents/capability/{capabilityName}", async (string capabilityName, [FromServices] IAgentRegistryService registry) =>
        {
            var agents = await registry.GetAgentsByCapabilityAsync(capabilityName);
            return Results.Ok(agents);
        });

        group.MapPost("/agents", async ([FromBody] RegisterAgentRequest request, [FromServices] IAgentRegistryService registry) =>
        {
            var result = await registry.RegisterOrUpdateAgentAsync(request);
            return Results.Created($"/api/registry/agents/{result.Id}", result);
        });

        group.MapDelete("/agents/{id}", async (string id, [FromServices] IAgentRegistryService registry) =>
        {
            var success = await registry.DeactivateAgentAsync(id);
            return success ? Results.Ok(new { message = $"Agent '{id}' deactivated" }) : Results.NotFound();
        });

        group.MapPost("/heartbeat/{id}", async (string id, [FromServices] IAgentRegistryService registry) =>
        {
            var success = await registry.RecordHeartbeatAsync(id);
            return success ? Results.Ok(new { message = "Heartbeat recorded" }) : Results.NotFound();
        });

        group.MapPost("/seed", async ([FromServices] IAgentRegistryService registry) =>
        {
            await registry.SeedDefaultAgentsAsync();
            var agents = await registry.GetAllActiveAgentsAsync();
            return Results.Ok(new { message = "Default registry seeded into SQLite", agents });
        });
    }
}
