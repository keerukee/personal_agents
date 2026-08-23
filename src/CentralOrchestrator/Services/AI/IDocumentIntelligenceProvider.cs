using Common.Contracts;

namespace CentralOrchestrator.Services.AI;

public interface IDocumentIntelligenceProvider
{
    string ProviderName { get; }
    Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(DocumentAnalysisRequest request, CancellationToken cancellationToken = default);
}
