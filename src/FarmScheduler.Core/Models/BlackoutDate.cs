namespace FarmScheduler.Core.Models;

public class BlackoutDate
{
    /// <summary>Unique identifier — typically the date string or date+barn+shift composite.</summary>
    public string Id { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    /// <summary>Optional description (e.g., "Christmas Day").</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional barn scope. Null means all barns are blacked out.</summary>
    public Barn? Barn { get; set; }

    /// <summary>Optional shift scope. Null means all shifts are blacked out.</summary>
    public ShiftTime? Shift { get; set; }
}
