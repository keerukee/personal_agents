using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Contracts;

public record McpRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0",
    [property: JsonPropertyName("id")] string Id = "",
    [property: JsonPropertyName("method")] string Method = "",
    [property: JsonPropertyName("params")] JsonElement? Params = null
);

public record McpResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0",
    [property: JsonPropertyName("id")] string Id = "",
    [property: JsonPropertyName("result")] McpResult? Result = null,
    [property: JsonPropertyName("error")] McpError? Error = null
);

public record McpResult(
    [property: JsonPropertyName("content")] List<McpContentItem>? Content = null,
    [property: JsonPropertyName("isError")] bool IsError = false
);

public record McpContentItem(
    [property: JsonPropertyName("type")] string Type = "text",
    [property: JsonPropertyName("text")] string Text = ""
);

public record McpError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null
);

public record McpToolCallParams(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] Dictionary<string, object>? Arguments = null
);

public record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema
);
