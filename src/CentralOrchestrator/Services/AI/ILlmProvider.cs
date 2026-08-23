using Common.Contracts;

namespace CentralOrchestrator.Services.AI;

public interface ILlmProvider
{
    string ProviderName { get; }
    Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);
}
