using FluentAssertions;
using Moq;
using FarmScheduler.Functions.Functions;
using FarmScheduler.Functions.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Tests.Functions;

public class DataCleanupFunctionTests
{
    private readonly Mock<IAvailabilityRepository> _mockAvailabilityRepo;
    private readonly Mock<IBlackoutRepository> _mockBlackoutRepo;
    private readonly DataCleanupFunction _function;

    public DataCleanupFunctionTests()
    {
        _mockAvailabilityRepo = new Mock<IAvailabilityRepository>();
        _mockBlackoutRepo = new Mock<IBlackoutRepository>();
        var logger = new Mock<ILogger<DataCleanupFunction>>();
        _function = new DataCleanupFunction(_mockAvailabilityRepo.Object, _mockBlackoutRepo.Object, logger.Object);
    }

    [Fact]
    public async Task Run_DeletesExpiredAvailability()
    {
        var timerInfo = new TimerInfo();

        await _function.Run(timerInfo);

        _mockAvailabilityRepo.Verify(
            x => x.DeleteExpiredAsync(It.Is<DateOnly>(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))),
            Times.Once);
    }

    [Fact]
    public async Task Run_DeletesExpiredBlackouts()
    {
        var timerInfo = new TimerInfo();

        await _function.Run(timerInfo);

        _mockBlackoutRepo.Verify(
            x => x.DeleteExpiredAsync(It.Is<DateOnly>(d => d == DateOnly.FromDateTime(DateTime.UtcNow))),
            Times.Once);
    }

    [Fact]
    public async Task Run_UsesCorrectRetentionPeriodForAvailability()
    {
        var timerInfo = new TimerInfo();

        await _function.Run(timerInfo);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedCutoff = today.AddDays(-30);

        _mockAvailabilityRepo.Verify(
            x => x.DeleteExpiredAsync(expectedCutoff),
            Times.Once);
    }
}
