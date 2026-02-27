namespace FarmScheduler.Core.Models;

public class BarnConfig
{
    public Barn Barn { get; set; }
    public int WorkersPerShift { get; set; } = 1;
}
