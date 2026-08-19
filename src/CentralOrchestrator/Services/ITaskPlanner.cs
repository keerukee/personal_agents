using Common.Contracts;

namespace CentralOrchestrator.Services;

public interface ITaskPlanner
{
    Task<TaskPlan> CreatePlanAsync(AgentEventMessage eventMessage, CancellationToken cancellationToken = default);
    Task<TaskPlan> ExecutePlanAsync(TaskPlan plan, CancellationToken cancellationToken = default);
}
