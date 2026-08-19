using Common.Contracts;

namespace CentralOrchestrator.Services;

public interface IHumanTaskService
{
    Task<List<HumanTaskDto>> GetTasksAsync(string? status = null);
    Task<HumanTaskDto?> GetTaskByIdAsync(int id);
    Task<HumanTaskDto> CreateTaskAsync(CreateHumanTaskRequest request);
    Task<bool> UpdateTaskStatusAsync(int id, UpdateTaskStatusRequest request);
    Task<bool> DeleteTaskAsync(int id);
}
