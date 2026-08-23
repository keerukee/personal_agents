using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralOrchestrator.Data.Entities;

[Table("AgentTasks")]
public class AgentTaskEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string TaskGuid { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string ParentEventGuid { get; set; } = string.Empty;

    public int StepOrder { get; set; } = 1;

    [Required]
    public string TargetAgentId { get; set; } = string.Empty;

    [Required]
    public string Action { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    [Required]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed, Skipped

    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
