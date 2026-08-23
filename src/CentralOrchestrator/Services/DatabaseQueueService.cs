using CentralOrchestrator.Data;
using CentralOrchestrator.Data.Entities;
using Common.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CentralOrchestrator.Services;

public class DatabaseQueueService : IDatabaseQueueService
{
    private readonly AgentRegistryDbContext _dbContext;
    private readonly ILogger<DatabaseQueueService> _logger;

    public DatabaseQueueService(AgentRegistryDbContext dbContext, ILogger<DatabaseQueueService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<InboundEventDto> CreateInboundEventAsync(CreateInboundEventRequest request)
    {
        var entity = new InboundEventEntity
        {
            EventGuid = Guid.NewGuid().ToString("N"),
            Source = request.Source,
            Prompt = request.Prompt,
            DataJson = string.IsNullOrWhiteSpace(request.DataJson) ? "{}" : request.DataJson,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.InboundEvents.Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Enqueued new Inbound Event '{EventGuid}' from Source '{Source}'", entity.EventGuid, entity.Source);
        return MapEventDto(entity);
    }

    public async Task<List<InboundEventDto>> GetPendingInboundEventsAsync(int limit = 10)
    {
        var entities = await _dbContext.InboundEvents
            .AsNoTracking()
            .Where(e => e.Status == "Pending")
            .OrderBy(e => e.Id)
            .Take(limit)
            .ToListAsync();

        return entities.Select(MapEventDto).ToList();
    }

    public async Task<List<InboundEventDto>> GetAllInboundEventsAsync(int limit = 50)
    {
        var entities = await _dbContext.InboundEvents
            .AsNoTracking()
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync();

        return entities.Select(MapEventDto).ToList();
    }

    public async Task<bool> UpdateInboundEventStatusAsync(string eventGuid, string status)
    {
        var entity = await _dbContext.InboundEvents.FirstOrDefaultAsync(e => e.EventGuid == eventGuid);
        if (entity == null) return false;

        entity.Status = status;
        if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            entity.ProcessedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<AgentTaskDto>> CreateAgentTasksAsync(string parentEventGuid, List<CreateAgentTaskRequest> taskRequests)
    {
        var entities = new List<AgentTaskEntity>();

        foreach (var req in taskRequests)
        {
            var task = new AgentTaskEntity
            {
                TaskGuid = Guid.NewGuid().ToString("N"),
                ParentEventGuid = parentEventGuid,
                StepOrder = req.StepOrder,
                TargetAgentId = req.TargetAgentId,
                Action = req.Action,
                PayloadJson = string.IsNullOrWhiteSpace(req.PayloadJson) ? "{}" : req.PayloadJson,
                Status = req.StepOrder == 1 ? "Pending" : "PendingDependency",
                CreatedAt = DateTimeOffset.UtcNow
            };
            entities.Add(task);
        }

        _dbContext.AgentTasks.AddRange(entities);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Enqueued {Count} AgentTasks for Parent Event '{ParentEventGuid}'", entities.Count, parentEventGuid);
        return entities.Select(MapTaskDto).ToList();
    }

    public async Task<List<AgentTaskDto>> GetPendingTasksForAgentAsync(string agentId, int limit = 10)
    {
        var entities = await _dbContext.AgentTasks
            .AsNoTracking()
            .Where(t => t.TargetAgentId == agentId && t.Status == "Pending")
            .OrderBy(t => t.StepOrder)
            .ThenBy(t => t.Id)
            .Take(limit)
            .ToListAsync();

        return entities.Select(MapTaskDto).ToList();
    }

    public async Task<List<AgentTaskDto>> GetAllAgentTasksAsync(string? agentId = null, string? status = null, int limit = 50)
    {
        var query = _dbContext.AgentTasks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            query = query.Where(t => t.TargetAgentId == agentId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status.ToLower() == status.ToLower());
        }

        var entities = await query.OrderByDescending(t => t.Id).Take(limit).ToListAsync();
        return entities.Select(MapTaskDto).ToList();
    }

    public async Task<bool> ClaimTaskAsync(string taskGuid)
    {
        var entity = await _dbContext.AgentTasks.FirstOrDefaultAsync(t => t.TaskGuid == taskGuid);
        if (entity == null || entity.Status != "Pending") return false;

        entity.Status = "InProgress";
        entity.StartedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Task '{TaskGuid}' claimed by Agent '{AgentId}'", taskGuid, entity.TargetAgentId);
        return true;
    }

    public async Task<bool> UpdateTaskResultAsync(string taskGuid, UpdateTaskResultRequest request)
    {
        var entity = await _dbContext.AgentTasks.FirstOrDefaultAsync(t => t.TaskGuid == taskGuid);
        if (entity == null) return false;

        entity.Status = request.Status;
        entity.ResultJson = request.ResultJson;
        entity.ErrorMessage = request.ErrorMessage;
        entity.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Task '{TaskGuid}' completed with Status '{Status}'", taskGuid, request.Status);
        return true;
    }

    public async Task<bool> UpdateTaskPayloadAndStatusAsync(string taskGuid, string newPayloadJson, string status)
    {
        var entity = await _dbContext.AgentTasks.FirstOrDefaultAsync(t => t.TaskGuid == taskGuid);
        if (entity == null) return false;

        entity.PayloadJson = newPayloadJson;
        entity.Status = status;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Updated Task '{TaskGuid}' Payload and Status to '{Status}'", taskGuid, status);
        return true;
    }

    public async Task<List<AgentTaskDto>> GetTasksForEventAsync(string parentEventGuid)
    {
        var entities = await _dbContext.AgentTasks
            .AsNoTracking()
            .Where(t => t.ParentEventGuid == parentEventGuid)
            .OrderBy(t => t.StepOrder)
            .ToListAsync();

        return entities.Select(MapTaskDto).ToList();
    }

    private static InboundEventDto MapEventDto(InboundEventEntity entity)
    {
        return new InboundEventDto(
            entity.Id,
            entity.EventGuid,
            entity.Source,
            entity.Prompt,
            entity.DataJson,
            entity.Status,
            entity.CreatedAt,
            entity.ProcessedAt
        );
    }

    private static AgentTaskDto MapTaskDto(AgentTaskEntity entity)
    {
        return new AgentTaskDto(
            entity.Id,
            entity.TaskGuid,
            entity.ParentEventGuid,
            entity.StepOrder,
            entity.TargetAgentId,
            entity.Action,
            entity.PayloadJson,
            entity.Status,
            entity.ResultJson,
            entity.ErrorMessage,
            entity.CreatedAt,
            entity.StartedAt,
            entity.CompletedAt
        );
    }
}
