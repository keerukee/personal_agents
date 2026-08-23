using Common.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace CentralOrchestrator.Services.AI;

public class AzureAiFoundryLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureAiFoundryLlmProvider> _logger;

    public string ProviderName => "Office (Azure AI Foundry)";

    public AzureAiFoundryLlmProvider(HttpClient httpClient, IConfiguration config, ILogger<AzureAiFoundryLlmProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = _config["AiSettings:Office:AzureAiFoundryEndpoint"];
        var apiKey = _config["AiSettings:Office:AzureAiFoundryApiKey"] ?? Environment.GetEnvironmentVariable("AZURE_AI_FOUNDRY_KEY");

        _logger.LogInformation("[Office Mode - Azure AI Foundry] Calling endpoint '{Endpoint}'", endpoint);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("YOUR_AZURE_KEY", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[Azure AI Foundry] No valid Azure API key found. Returning simulated Azure response for local development.");
            return new LlmCompletionResponse(
                ResponseText: $"[Simulated Azure AI Foundry Response]: Enterprise LLM completion for prompt: \"{request.Prompt}\"",
                ModelName: "gpt-4o-enterprise",
                TokensUsed: 180,
                Provider: ProviderName
            );
        }

        try
        {
            var reqMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            reqMessage.Headers.Add("api-key", apiKey);

            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = request.SystemInstruction },
                    new { role = "user", content = request.Prompt }
                },
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

            reqMessage.Content = JsonContent.Create(payload);
            var response = await _httpClient.SendAsync(reqMessage, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return new LlmCompletionResponse(
                ResponseText: text,
                ModelName: "gpt-4o-enterprise",
                TokensUsed: 310,
                Provider: ProviderName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Azure AI Foundry Error] Endpoint call failed.");
            throw;
        }
    }
}
