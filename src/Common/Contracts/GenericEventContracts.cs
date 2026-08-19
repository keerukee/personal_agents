using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Contracts;

public record AgentEventMessage(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("data")] JsonElement Data
);

public record GenericAgentResponse(
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("output")] JsonElement Output,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage
);
