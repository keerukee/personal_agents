using System.Text.Json.Serialization;

namespace Common.Contracts;

public record HumanTaskDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("assignedAgentId")] string? AssignedAgentId,
    [property: JsonPropertyName("status")] string Status, // e.g., "PendingHumanAction", "InReview", "Completed", "Cancelled"
    [property: JsonPropertyName("priority")] string Priority, // "High", "Medium", "Low"
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt
);

public record CreateHumanTaskRequest(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("assignedAgentId")] string? AssignedAgentId,
    [property: JsonPropertyName("priority")] string Priority = "Medium"
);

public record UpdateTaskStatusRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("notes")] string? Notes = null
);
