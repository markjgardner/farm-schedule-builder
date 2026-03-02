using FarmScheduler.Functions.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class DataCleanupFunction
{
    private readonly IAvailabilityRepository _availabilityRepository;
    private readonly IBlackoutRepository _blackoutRepository;
    private readonly ILogger<DataCleanupFunction> _logger;

    // Keep availability data for 30 days after the window ends
    private const int AvailabilityRetentionDays = 30;

    public DataCleanupFunction(
        IAvailabilityRepository availabilityRepository,
        IBlackoutRepository blackoutRepository,
        ILogger<DataCleanupFunction> logger)
    {
        _availabilityRepository = availabilityRepository;
        _blackoutRepository = blackoutRepository;
        _logger = logger;
    }

    [Function("DataCleanup")]
    public async Task Run(
        [TimerTrigger("0 0 3 * * 0")] TimerInfo timerInfo) // Weekly on Sunday at 3 AM UTC
    {
        _logger.LogInformation("Data cleanup started at {Time}", DateTime.UtcNow);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Delete availability windows older than retention period
        var availabilityCutoff = today.AddDays(-AvailabilityRetentionDays);
        await _availabilityRepository.DeleteExpiredAsync(availabilityCutoff);
        _logger.LogInformation("Deleted availability data older than {Cutoff}", availabilityCutoff);

        // Delete blackout dates that have passed
        await _blackoutRepository.DeleteExpiredAsync(today);
        _logger.LogInformation("Deleted blackout dates before {Today}", today);
    }
}
