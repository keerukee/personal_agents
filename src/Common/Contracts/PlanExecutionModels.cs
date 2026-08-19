using System.Text.Json.Serialization;

namespace Common.Contracts;

public record TaskPlan(
    [property: JsonPropertyName("planId")] string PlanId,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("steps")] List<TaskStep> Steps,
    [property: JsonPropertyName("status")] string Status
);

public record TaskStep(
    [property: JsonPropertyName("stepId")] int StepId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("parameters")] Dictionary<string, object> Parameters,
    [property: JsonPropertyName("dependsOn")] List<int> DependsOn,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("resultOutput")] string? ResultOutput
);
