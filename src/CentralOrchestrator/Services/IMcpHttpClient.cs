using Common.Contracts;

namespace CentralOrchestrator.Services;

public interface IMcpHttpClient
{
    Task<McpResponse> SendRequestAsync(string endpointUrl, McpRequest request, CancellationToken cancellationToken = default);
    Task<McpResponse> CallToolAsync(string endpointUrl, string toolName, Dictionary<string, object>? arguments, CancellationToken cancellationToken = default);
}
