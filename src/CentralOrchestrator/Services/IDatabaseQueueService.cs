using Common.Contracts;

namespace CentralOrchestrator.Services;

public interface IDatabaseQueueService
{
    Task<InboundEventDto> CreateInboundEventAsync(CreateInboundEventRequest request);
    Task<List<InboundEventDto>> GetPendingInboundEventsAsync(int limit = 10);
    Task<List<InboundEventDto>> GetAllInboundEventsAsync(int limit = 50);
    Task<bool> UpdateInboundEventStatusAsync(string eventGuid, string status);
    Task<List<AgentTaskDto>> CreateAgentTasksAsync(string parentEventGuid, List<CreateAgentTaskRequest> taskRequests);
    Task<List<AgentTaskDto>> GetPendingTasksForAgentAsync(string agentId, int limit = 10);
    Task<List<AgentTaskDto>> GetAllAgentTasksAsync(string? agentId = null, string? status = null, int limit = 50);
    Task<bool> ClaimTaskAsync(string taskGuid);
    Task<bool> UpdateTaskResultAsync(string taskGuid, UpdateTaskResultRequest request);
    Task<List<AgentTaskDto>> GetTasksForEventAsync(string parentEventGuid);
}
