using Common.Contracts;

namespace CentralOrchestrator.Services;

public interface IAgentRegistryService
{
    Task<List<AgentRegistration>> GetAllActiveAgentsAsync();
    Task<AgentRegistration?> GetAgentByIdAsync(string id);
    Task<List<AgentRegistration>> GetAgentsByCapabilityAsync(string capabilityName);
    Task<AgentRegistration> RegisterOrUpdateAgentAsync(RegisterAgentRequest request);
    Task<bool> DeactivateAgentAsync(string id);
    Task<bool> RecordHeartbeatAsync(string id);
    Task SeedDefaultAgentsAsync();
}
