using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class TeamSizeTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);  // Monday
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);   // Sunday (14 days)
    private const int DaysInWindow = 14;
    private const int SlotsPerDay = 4; // 2 barns × 2 shifts
    private const int TotalSlots = DaysInWindow * SlotsPerDay; // 56

    private readonly SchedulingService _schedulingService;

    public TeamSizeTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void EightWorkers_AllAvailable_FairnessValidation()
    {
        // Arrange
        var workers = CreateWorkers(8);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Assert — 56 slots / 8 workers = 7 each; allow tight range 5-9
        AssertNoConflicts(schedule);
        AssertNoUnfilled(schedule);

        var counts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();

        counts.Should().HaveCount(8);
        counts.Should().OnlyContain(c => c >= 5 && c <= 9,
            "each of 8 workers should get 5-9 shifts out of 56 total");
    }

    [Fact]
    public void EightWorkers_VariedConstraints_RealisticScenario()
    {
        // Arrange
        var workers = CreateWorkers(8);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // w1, w2: fully available (no changes needed)

        // w3: MorningOnly all days
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w3", date, AvailabilityStatus.MorningOnly);

        // w4: EveningOnly all days
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w4", date, AvailabilityStatus.EveningOnly);

        // w5: MorningOnly weekdays, NotAvailable weekends
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                SetWorkerAvailability(avail, "w5", date, AvailabilityStatus.NotAvailable);
            else
                SetWorkerAvailability(avail, "w5", date, AvailabilityStatus.MorningOnly);
        }

        // w6: NotAvailable Mon-Fri, Available weekends only
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                SetWorkerAvailability(avail, "w6", date, AvailabilityStatus.NotAvailable);
        }

        // w7: Available first week only (NotAvailable second week)
        for (var date = WindowStart.AddDays(7); date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w7", date, AvailabilityStatus.NotAvailable);

        // w8: Available second week only (NotAvailable first week)
        for (var date = WindowStart; date < WindowStart.AddDays(7); date = date.AddDays(1))
            SetWorkerAvailability(avail, "w8", date, AvailabilityStatus.NotAvailable);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Assert
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);
        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoUnfilled(schedule);
        AssertReasonableFairness(schedule, 8);
    }

    [Fact]
    public void TwoWorkers_AllAvailable_MaxLoad()
    {
        // Arrange
        var workers = CreateWorkers(2);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Assert — 2 workers can fill all 56 slots (2 barns × 2 shifts × 14 days)
        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertNoUnfilled(schedule);

        var counts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();

        counts.Should().HaveCount(2);
        counts.Should().OnlyContain(c => c >= 26 && c <= 30,
            "each of 2 workers should get ~28 shifts");
    }

    [Fact]
    public void OneWorker_PartialAvailability_RestUnfilled()
    {
        // Arrange
        var workers = CreateWorkers(1);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // NotAvailable days 8-14 (second week)
        for (var date = WindowStart.AddDays(7); date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w1", date, AvailabilityStatus.NotAvailable);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Assert
        // Days 1-7: worker fills 1 slot per timeslot (can't be at 2 barns at once)
        //   = 2 filled per day × 7 days = 14 filled, 14 unfilled (2 per day)
        // Days 8-14: 0 filled, 28 unfilled (4 per day × 7 days)
        // Total: 14 filled, 42 unfilled
        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);

        int filled = schedule.Assignments.Count(a => a.WorkerId != "");
        int unfilled = schedule.Assignments.Count(a => a.WorkerName == "UNFILLED");

        filled.Should().Be(14);
        unfilled.Should().Be(42);
    }
}
