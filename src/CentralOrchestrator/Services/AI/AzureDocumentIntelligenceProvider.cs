using Common.Contracts;

namespace CentralOrchestrator.Services.AI;

public class AzureDocumentIntelligenceProvider : IDocumentIntelligenceProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureDocumentIntelligenceProvider> _logger;

    public string ProviderName => "Office (Azure Document Intelligence)";

    public AzureDocumentIntelligenceProvider(HttpClient httpClient, IConfiguration config, ILogger<AzureDocumentIntelligenceProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(DocumentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Office Mode - Azure Document Intelligence] Extracting enterprise layout for file '{FileName}' ({Size} bytes)", request.FileName, request.FileBytes.Length);

        // Simulated structured table extraction for Office Azure mode
        var sampleTables = new List<DocumentTable>
        {
            new(
                TableIndex: 1,
                RowCount: 4,
                ColumnCount: 4,
                Rows: new List<List<string>>
                {
                    new() { "Cost Center", "GL Account", "Description", "Total ($)" },
                    new() { "CC-901", "6100-20", "Enterprise Server License", "12,500.00" },
                    new() { "CC-901", "6100-30", "Azure Cloud Infrastructure", "18,250.00" },
                    new() { "CC-902", "6100-40", "Security Audit Services", "5,000.00" }
                }
            )
        };

        return await Task.FromResult(new DocumentAnalysisResponse(
            ExtractedText: $"[Office - Azure Document Intelligence Layout from {request.FileName}]\nPO #OFFICE-2026-991\nTotal PO Value: $35,750.00\nDepartment: Enterprise IT",
            Tables: sampleTables,
            Summary: $"Extracted 1 enterprise table with 4 rows from {request.FileName} using Azure Document Intelligence.",
            Provider: ProviderName
        ));
    }
}
