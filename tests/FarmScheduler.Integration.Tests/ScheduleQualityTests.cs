using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class ScheduleQualityTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);
    private const int DaysInWindow = 14;

    private readonly SchedulingService _schedulingService;

    public ScheduleQualityTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void ClusteringEffectiveness_WorkersGetMultipleShiftsPerDay()
    {
        // Arrange: 3 workers, all available for 14 days (4 slots/day means at least one worker
        // must get 2+ shifts per day, and the clustering bonus ×5 should reinforce this grouping)
        var workers = CreateWorkers(3);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: at least 40% of worker-day pairs have 2+ shifts
        AssertNoConflicts(schedule);
        AssertClusteringRate(schedule, 0.40);
    }

    [Fact]
    public void BarnDistribution_NoWorkerDominatesOneBarn()
    {
        // Arrange: 6 workers, all available
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: barn consistency bonus (×2) nudges toward one barn but shouldn't exceed 80% skew
        AssertNoConflicts(schedule);
        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertBarnBalance(schedule, 0.80);
    }

    [Fact]
    public void FairnessUnderConstraintAsymmetry_UnconstrainedWorkersCompensate()
    {
        // Arrange: 6 workers; w3-w6 unavailable in the second week
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var secondWeekStart = WindowStart.AddDays(7); // 2025-01-13
        for (var date = secondWeekStart; date <= WindowEnd; date = date.AddDays(1))
        {
            for (var i = 3; i <= 6; i++)
            {
                SetWorkerAvailability(availability, $"w{i}", date, AvailabilityStatus.NotAvailable);
            }
        }

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: w1/w2 pick up all second-week slots so they should have >12 shifts each;
        // w3-w6 share first-week slots and should each have at least 3
        var shiftCounts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .ToDictionary(g => g.Key, g => g.Count());

        shiftCounts["w1"].Should().BeGreaterThan(12, "w1 is available both weeks and should compensate");
        shiftCounts["w2"].Should().BeGreaterThan(12, "w2 is available both weeks and should compensate");

        for (var i = 3; i <= 6; i++)
        {
            shiftCounts[$"w{i}"].Should().BeGreaterOrEqualTo(3,
                $"w{i} is available the first week and should still get some shifts");
        }
    }

    [Fact]
    public void MinimizeSingleShiftDays_WithSurplusWorkers()
    {
        // Arrange: 6 workers, all available — clustering bonus should reduce single-shift days
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: single-shift days should be less than 85% of all worker-day pairs with shifts.
        // With 6 workers competing for 4 slots/day, most worker-days will have only 1 shift,
        // but the clustering bonus should still produce some multi-shift days (>15%).
        var totalWorkerDayPairs = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.WorkerId, a.Date))
            .Count();

        var singleShiftDays = CountSingleShiftDays(schedule);

        singleShiftDays.Should().BeLessThan(
            (int)(totalWorkerDayPairs * 0.85),
            "clustering bonus should produce some multi-shift worker-days even with surplus workers");
    }
}
