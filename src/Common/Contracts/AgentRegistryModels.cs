using System.Text.Json.Serialization;

namespace Common.Contracts;

public record AgentRegistration(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("endpointUrl")] string EndpointUrl,
    [property: JsonPropertyName("transportType")] string TransportType,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("registeredAt")] DateTimeOffset RegisteredAt,
    [property: JsonPropertyName("lastHeartbeat")] DateTimeOffset LastHeartbeat,
    [property: JsonPropertyName("capabilities")] List<AgentCapabilityDto> Capabilities
);

public record AgentCapabilityDto(
    [property: JsonPropertyName("capabilityName")] string CapabilityName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parametersJsonSchema")] string ParametersJsonSchema
);

public record RegisterAgentRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("endpointUrl")] string EndpointUrl,
    [property: JsonPropertyName("transportType")] string TransportType,
    [property: JsonPropertyName("capabilities")] List<AgentCapabilityDto> Capabilities
);
