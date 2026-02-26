using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public interface IWorkerRepository
{
    Task<IReadOnlyList<Worker>> GetAllActiveAsync();
    Task<IReadOnlyList<Worker>> GetAllAsync();
    Task<Worker?> GetByIdAsync(string workerId);
    Task UpsertAsync(Worker worker);
    Task DeleteAsync(string workerId);
}
