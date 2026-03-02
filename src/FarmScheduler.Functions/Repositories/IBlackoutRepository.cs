using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public interface IBlackoutRepository
{
    Task<IReadOnlyList<BlackoutDate>> GetAllAsync();
    Task<IReadOnlyList<BlackoutDate>> GetForWindowAsync(DateOnly start, DateOnly end);
    Task UpsertAsync(BlackoutDate blackout);
    Task DeleteAsync(string id);
    Task DeleteExpiredAsync(DateOnly before);
}
