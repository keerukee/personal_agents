using Common.Contracts;

namespace CentralOrchestrator.Services.AI;

public interface IAiProviderFactory
{
    string ActiveEnvironment { get; }
    ILlmProvider GetLlmProvider();
    IDocumentIntelligenceProvider GetDocumentIntelligenceProvider();
}

public class AiProviderFactory : IAiProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<AiProviderFactory> _logger;

    public string ActiveEnvironment => _config["AiSettings:Environment"] ?? "Home";

    public AiProviderFactory(IServiceProvider serviceProvider, IConfiguration config, ILogger<AiProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    public ILlmProvider GetLlmProvider()
    {
        var env = ActiveEnvironment;
        _logger.LogInformation("Resolving LLM Provider for Environment '{Environment}'", env);

        if (env.Equals("Office", StringComparison.OrdinalIgnoreCase))
        {
            return _serviceProvider.GetRequiredService<AzureAiFoundryLlmProvider>();
        }

        return _serviceProvider.GetRequiredService<GoogleGeminiLlmProvider>();
    }

    public IDocumentIntelligenceProvider GetDocumentIntelligenceProvider()
    {
        var env = ActiveEnvironment;
        _logger.LogInformation("Resolving Document Intelligence Provider for Environment '{Environment}'", env);

        if (env.Equals("Office", StringComparison.OrdinalIgnoreCase))
        {
            return _serviceProvider.GetRequiredService<AzureDocumentIntelligenceProvider>();
        }

        return _serviceProvider.GetRequiredService<GoogleDocumentAiProvider>();
    }
}
