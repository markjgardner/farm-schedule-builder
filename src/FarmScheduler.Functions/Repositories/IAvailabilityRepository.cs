using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public interface IAvailabilityRepository
{
    Task<IReadOnlyList<Availability>> GetByWindowAsync(string windowStart);
    Task<IReadOnlyList<Availability>> GetByWindowAndWorkerAsync(string windowStart, string workerId);
    Task UpsertAsync(string windowStart, Availability availability);
    Task UpsertBatchAsync(string windowStart, IReadOnlyList<Availability> availability);
    Task DeleteExpiredAsync(DateOnly before);
}
