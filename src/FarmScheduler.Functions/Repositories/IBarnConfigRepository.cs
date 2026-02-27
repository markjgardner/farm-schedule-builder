using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public interface IBarnConfigRepository
{
    Task<IReadOnlyList<BarnConfig>> GetAllAsync();
    Task<BarnConfig?> GetAsync(Barn barn);
    Task UpsertAsync(BarnConfig config);
}
