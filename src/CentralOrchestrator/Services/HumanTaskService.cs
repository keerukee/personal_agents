using CentralOrchestrator.Data;
using CentralOrchestrator.Data.Entities;
using Common.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CentralOrchestrator.Services;

public class HumanTaskService : IHumanTaskService
{
    private readonly AgentRegistryDbContext _dbContext;
    private readonly ILogger<HumanTaskService> _logger;

    public HumanTaskService(AgentRegistryDbContext dbContext, ILogger<HumanTaskService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<HumanTaskDto>> GetTasksAsync(string? status = null)
    {
        var query = _dbContext.HumanTasks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status.ToLower() == status.ToLower());
        }

        var entities = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<HumanTaskDto?> GetTaskByIdAsync(int id)
    {
        var entity = await _dbContext.HumanTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<HumanTaskDto> CreateTaskAsync(CreateHumanTaskRequest request)
    {
        var entity = new HumanTaskEntity
        {
            EventId = request.EventId,
            Title = request.Title,
            Description = request.Description,
            AssignedAgentId = request.AssignedAgentId,
            Status = "PendingHumanAction",
            Priority = request.Priority ?? "Medium",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.HumanTasks.Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Logged new Human Action Task #{TaskId} '{Title}' from Event '{EventId}'", entity.Id, entity.Title, entity.EventId);
        return MapToDto(entity);
    }

    public async Task<bool> UpdateTaskStatusAsync(int id, UpdateTaskStatusRequest request)
    {
        var entity = await _dbContext.HumanTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return false;

        entity.Status = request.Status;
        if (request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            entity.CompletedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Updated Task #{TaskId} status to '{Status}'", id, request.Status);
        return true;
    }

    public async Task<bool> DeleteTaskAsync(int id)
    {
        var entity = await _dbContext.HumanTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return false;

        _dbContext.HumanTasks.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static HumanTaskDto MapToDto(HumanTaskEntity entity)
    {
        return new HumanTaskDto(
            entity.Id,
            entity.EventId,
            entity.Title,
            entity.Description,
            entity.AssignedAgentId,
            entity.Status,
            entity.Priority,
            entity.CreatedAt,
            entity.CompletedAt
        );
    }
}
