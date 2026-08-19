using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralOrchestrator.Data.Entities;

[Table("HumanTasks")]
public class HumanTaskEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string EventId { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? AssignedAgentId { get; set; }

    [Required]
    public string Status { get; set; } = "PendingHumanAction"; // PendingHumanAction, InReview, Completed, Cancelled

    public string Priority { get; set; } = "Medium";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}
