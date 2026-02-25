namespace FarmScheduler.Core.Models;

public class Schedule
{
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<ShiftAssignment> Assignments { get; set; } = new();
}
