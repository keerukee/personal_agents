using Common.Contracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CentralOrchestrator.Services;

public class StreamableMcpHttpClient : IMcpHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StreamableMcpHttpClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public StreamableMcpHttpClient(HttpClient httpClient, ILogger<StreamableMcpHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<McpResponse> SendRequestAsync(string endpointUrl, McpRequest request, CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream")); // Optional streaming support header

        _logger.LogInformation("Sending Streamable HTTP MCP request to {Endpoint} with method {Method}", endpointUrl, request.Method);

        using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        await using var responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        var response = await JsonSerializer.DeserializeAsync<McpResponse>(responseStream, JsonOptions, cancellationToken);

        if (response == null)
        {
            throw new InvalidOperationException($"Empty response received from MCP Server at {endpointUrl}");
        }

        return response;
    }

    public async Task<McpResponse> CallToolAsync(string endpointUrl, string toolName, Dictionary<string, object>? arguments, CancellationToken cancellationToken = default)
    {
        var requestId = $"req_{Guid.NewGuid():N}";
        var toolParamsObj = new Dictionary<string, object>
        {
            ["name"] = toolName,
            ["arguments"] = arguments ?? new Dictionary<string, object>()
        };

        var jsonParams = JsonSerializer.SerializeToElement(toolParamsObj, JsonOptions);

        var request = new McpRequest(
            JsonRpc: "2.0",
            Id: requestId,
            Method: "tools/call",
            Params: jsonParams
        );

        return await SendRequestAsync(endpointUrl, request, cancellationToken);
    }
}
