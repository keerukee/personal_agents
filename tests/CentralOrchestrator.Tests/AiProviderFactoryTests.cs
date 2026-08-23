using CentralOrchestrator.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralOrchestrator.Tests;

public class AiProviderFactoryTests
{
    [Fact]
    public void GetLlmProvider_ReturnsGoogleGemini_WhenEnvironmentIsHome()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "AiSettings:Environment", "Home" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient<GoogleGeminiLlmProvider>();
        services.AddHttpClient<GoogleDocumentAiProvider>();
        services.AddHttpClient<AzureAiFoundryLlmProvider>();
        services.AddHttpClient<AzureDocumentIntelligenceProvider>();
        services.AddSingleton(NullLogger<AiProviderFactory>.Instance);
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAiProviderFactory>();

        var llm = factory.GetLlmProvider();
        var doc = factory.GetDocumentIntelligenceProvider();

        Assert.Equal("Home", factory.ActiveEnvironment);
        Assert.IsType<GoogleGeminiLlmProvider>(llm);
        Assert.IsType<GoogleDocumentAiProvider>(doc);
        Assert.Contains("Home", llm.ProviderName);
    }

    [Fact]
    public void GetLlmProvider_ReturnsAzureFoundry_WhenEnvironmentIsOffice()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "AiSettings:Environment", "Office" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient<GoogleGeminiLlmProvider>();
        services.AddHttpClient<GoogleDocumentAiProvider>();
        services.AddHttpClient<AzureAiFoundryLlmProvider>();
        services.AddHttpClient<AzureDocumentIntelligenceProvider>();
        services.AddSingleton(NullLogger<AiProviderFactory>.Instance);
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAiProviderFactory>();

        var llm = factory.GetLlmProvider();
        var doc = factory.GetDocumentIntelligenceProvider();

        Assert.Equal("Office", factory.ActiveEnvironment);
        Assert.IsType<AzureAiFoundryLlmProvider>(llm);
        Assert.IsType<AzureDocumentIntelligenceProvider>(doc);
        Assert.Contains("Office", llm.ProviderName);
    }
}
