namespace FarmScheduler.Core.Models;

public enum Barn
{
    Windhover,
    York
}

public enum ShiftTime
{
    Morning,
    Evening
}

public enum AvailabilityStatus
{
    Available,
    NotAvailable,
    MorningOnly,
    EveningOnly
}
