using Common.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace CentralOrchestrator.Services.AI;

public class GoogleGeminiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleGeminiLlmProvider> _logger;

    public string ProviderName => "Home (Google Gemini)";

    public GoogleGeminiLlmProvider(HttpClient httpClient, IConfiguration config, ILogger<GoogleGeminiLlmProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["AiSettings:Home:GoogleApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var modelName = _config["AiSettings:Home:ModelName"] ?? "gemini-2.5-flash";

        _logger.LogInformation("[Home Mode - Google Gemini] Calling model '{Model}'", modelName);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("YOUR_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[Google Gemini] No valid Google API key found. Returning simulated Gemini response for local development.");
            return new LlmCompletionResponse(
                ResponseText: $"[Simulated Google Gemini Response ({modelName})]: Processed prompt: \"{request.Prompt}\"",
                ModelName: modelName,
                TokensUsed: 120,
                Provider: ProviderName
            );
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = string.IsNullOrWhiteSpace(request.SystemInstruction) ? request.Prompt : $"{request.SystemInstruction}\n\n{request.Prompt}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = request.Temperature,
                    maxOutputTokens = request.MaxTokens
                }
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            return new LlmCompletionResponse(
                ResponseText: text,
                ModelName: modelName,
                TokensUsed: 250,
                Provider: ProviderName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Google Gemini Error] API call failed.");
            throw;
        }
    }
}
