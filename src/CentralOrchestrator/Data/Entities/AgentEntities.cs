using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralOrchestrator.Data.Entities;

[Table("Agents")]
public class AgentEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    public string EndpointUrl { get; set; } = string.Empty;

    [Required]
    public string TransportType { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

    public List<AgentCapabilityEntity> Capabilities { get; set; } = new();
}

[Table("AgentCapabilities")]
public class AgentCapabilityEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string AgentId { get; set; } = string.Empty;

    [ForeignKey(nameof(AgentId))]
    public AgentEntity? Agent { get; set; }

    [Required]
    public string CapabilityName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ParametersJsonSchema { get; set; } = "{}";
}
