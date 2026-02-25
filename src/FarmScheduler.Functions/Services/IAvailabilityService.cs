using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Services;

public interface IAvailabilityService
{
    Task<IReadOnlyList<Availability>> GetAvailabilityAsync(string windowStart, string? workerId = null);
    Task SetAvailabilityAsync(string windowStart, string workerId, IReadOnlyList<Availability> availability);
}
