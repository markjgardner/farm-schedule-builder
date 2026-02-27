using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Services;

public interface ISchedulingService
{
    Schedule GenerateSchedule(
        IReadOnlyList<Worker> workers,
        IReadOnlyList<Availability> availability,
        DateOnly windowStart,
        DateOnly windowEnd,
        IReadOnlyList<BarnConfig>? barnConfigs = null,
        IReadOnlyList<BlackoutDate>? blackouts = null);
}
