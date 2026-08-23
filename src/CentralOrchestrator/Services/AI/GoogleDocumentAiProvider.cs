using Common.Contracts;

namespace CentralOrchestrator.Services.AI;

public class GoogleDocumentAiProvider : IDocumentIntelligenceProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleDocumentAiProvider> _logger;

    public string ProviderName => "Home (Google Document AI / Gemini Multimodal)";

    public GoogleDocumentAiProvider(HttpClient httpClient, IConfiguration config, ILogger<GoogleDocumentAiProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(DocumentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Home Mode - Google Document AI] Processing file '{FileName}' ({Size} bytes)", request.FileName, request.FileBytes.Length);

        // Simulated structured table extraction for Home Google mode
        var sampleTables = new List<DocumentTable>
        {
            new(
                TableIndex: 1,
                RowCount: 3,
                ColumnCount: 3,
                Rows: new List<List<string>>
                {
                    new() { "Item", "Quantity", "Amount ($)" },
                    new() { "Home Office Desk", "1", "350.00" },
                    new() { "Ergonomic Chair", "1", "450.00" }
                }
            )
        };

        return await Task.FromResult(new DocumentAnalysisResponse(
            ExtractedText: $"[Home - Google Document AI Extracted Text from {request.FileName}]\nInvoice #88412\nTotal Amount: $800.00\nDate: 2026-08-23",
            Tables: sampleTables,
            Summary: $"Extracted 1 table and 3 invoice lines from {request.FileName} using Google Document AI.",
            Provider: ProviderName
        ));
    }
}
