using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralOrchestrator.Data.Entities;

[Table("InboundEvents")]
public class InboundEventEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string EventGuid { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public string DataJson { get; set; } = "{}";

    [Required]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public List<AgentTaskEntity> Tasks { get; set; } = new();
}
