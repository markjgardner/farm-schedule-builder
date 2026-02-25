namespace FarmScheduler.Core.Models;

public class Availability
{
    public string WorkerId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Available;
}
