namespace FarmScheduler.Core.Models;

public class ShiftAssignment
{
    public DateOnly Date { get; set; }
    public Barn Barn { get; set; }
    public ShiftTime Shift { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
}
