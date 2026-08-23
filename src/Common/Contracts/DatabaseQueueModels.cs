using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Contracts;

public record InboundEventDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("eventGuid")] string EventGuid,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("dataJson")] string DataJson,
    [property: JsonPropertyName("status")] string Status, // Pending, Processing, Completed, Failed
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("processedAt")] DateTimeOffset? ProcessedAt
);

public record CreateInboundEventRequest(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("dataJson")] string DataJson = "{}"
);

public record AgentTaskDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("taskGuid")] string TaskGuid,
    [property: JsonPropertyName("parentEventGuid")] string ParentEventGuid,
    [property: JsonPropertyName("stepOrder")] int StepOrder,
    [property: JsonPropertyName("targetAgentId")] string TargetAgentId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("payloadJson")] string PayloadJson,
    [property: JsonPropertyName("status")] string Status, // Pending, InProgress, Completed, Failed, Skipped
    [property: JsonPropertyName("resultJson")] string? ResultJson,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("startedAt")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt
);

public record CreateAgentTaskRequest(
    [property: JsonPropertyName("parentEventGuid")] string ParentEventGuid,
    [property: JsonPropertyName("stepOrder")] int StepOrder,
    [property: JsonPropertyName("targetAgentId")] string TargetAgentId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("payloadJson")] string PayloadJson
);

public record UpdateTaskResultRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("resultJson")] string? ResultJson = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null
);
