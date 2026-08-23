using System.Text.Json.Serialization;

namespace Common.Contracts;

public record LlmCompletionRequest(
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("systemInstruction")] string SystemInstruction = "",
    [property: JsonPropertyName("temperature")] double Temperature = 0.7,
    [property: JsonPropertyName("maxTokens")] int MaxTokens = 2048
);

public record LlmCompletionResponse(
    [property: JsonPropertyName("responseText")] string ResponseText,
    [property: JsonPropertyName("modelName")] string ModelName,
    [property: JsonPropertyName("tokensUsed")] int TokensUsed = 0,
    [property: JsonPropertyName("provider")] string Provider = ""
);

public record DocumentAnalysisRequest(
    [property: JsonPropertyName("fileBytes")] byte[] FileBytes,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("mimeType")] string MimeType = "application/pdf"
);

public record DocumentTable(
    [property: JsonPropertyName("tableIndex")] int TableIndex,
    [property: JsonPropertyName("rowCount")] int RowCount,
    [property: JsonPropertyName("columnCount")] int ColumnCount,
    [property: JsonPropertyName("rows")] List<List<string>> Rows
);

public record DocumentAnalysisResponse(
    [property: JsonPropertyName("extractedText")] string ExtractedText,
    [property: JsonPropertyName("tables")] List<DocumentTable> Tables,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("provider")] string Provider
);
