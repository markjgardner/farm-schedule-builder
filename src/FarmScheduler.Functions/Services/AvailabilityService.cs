using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;

namespace FarmScheduler.Functions.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IAvailabilityRepository _availabilityRepository;

    public AvailabilityService(IAvailabilityRepository availabilityRepository)
    {
        _availabilityRepository = availabilityRepository;
    }

    public async Task<IReadOnlyList<Availability>> GetAvailabilityAsync(string windowStart, string? workerId = null)
    {
        if (workerId != null)
            return await _availabilityRepository.GetByWindowAndWorkerAsync(windowStart, workerId);
        return await _availabilityRepository.GetByWindowAsync(windowStart);
    }

    public async Task SetAvailabilityAsync(string windowStart, string workerId, IReadOnlyList<Availability> availability)
    {
        foreach (var item in availability)
        {
            item.WorkerId = workerId;
        }
        await _availabilityRepository.UpsertBatchAsync(windowStart, availability);
    }
}
