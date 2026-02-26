using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class WindowBoundaryTests
{
    private const int FixedSeed = 42;
    private readonly SchedulingService _schedulingService;

    public WindowBoundaryTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void SingleDayWindow_FourSlots()
    {
        var windowStart = new DateOnly(2025, 1, 6);
        var windowEnd = new DateOnly(2025, 1, 6);

        var workers = CreateWorkers(4);
        var availability = CreateAllAvailable(workers, windowStart, windowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, availability, windowStart, windowEnd);

        AssertAllSlotsPresent(schedule, 1);
        AssertNoUnfilled(schedule);
        AssertNoConflicts(schedule);

        schedule.Assignments.Should().OnlyContain(a => a.Date == windowStart,
            "all assignments should fall on the single window day");
    }

    [Fact]
    public void OneWeekWindow_TwentyEightSlots()
    {
        var windowStart = new DateOnly(2025, 1, 6);
        var windowEnd = new DateOnly(2025, 1, 12);

        var workers = CreateWorkers(4);
        var availability = CreateAllAvailable(workers, windowStart, windowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, availability, windowStart, windowEnd);

        AssertAllSlotsPresent(schedule, 7);
        AssertNoUnfilled(schedule);
        AssertNoConflicts(schedule);
        AssertReasonableFairness(schedule, 4);
    }

    [Fact]
    public void FourWeekWindow_OneHundredTwelveSlots()
    {
        var windowStart = new DateOnly(2025, 1, 6);
        var windowEnd = new DateOnly(2025, 2, 2);

        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, windowStart, windowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, availability, windowStart, windowEnd);

        AssertAllSlotsPresent(schedule, 28);
        AssertNoUnfilled(schedule);
        AssertNoConflicts(schedule);
        AssertReasonableFairness(schedule, 6);
    }

    [Fact]
    public void WindowStartingMidWeek_NoDayOfWeekAssumptions()
    {
        var windowStart = new DateOnly(2025, 1, 8);  // Wednesday
        var windowEnd = new DateOnly(2025, 1, 21);   // Tuesday

        var workers = CreateWorkers(4);
        var availability = CreateAllAvailable(workers, windowStart, windowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, availability, windowStart, windowEnd);

        AssertAllSlotsPresent(schedule, 14);
        AssertNoUnfilled(schedule);
        AssertNoConflicts(schedule);

        // Verify all 14 dates are present in assignments
        var expectedDates = new List<DateOnly>();
        for (var d = windowStart; d <= windowEnd; d = d.AddDays(1))
            expectedDates.Add(d);

        var assignmentDates = schedule.Assignments.Select(a => a.Date).Distinct().OrderBy(d => d).ToList();
        assignmentDates.Should().BeEquivalentTo(expectedDates);

        // Verify both barns and both shifts appear every day
        foreach (var date in expectedDates)
        {
            var dayAssignments = schedule.Assignments.Where(a => a.Date == date).ToList();

            dayAssignments.Select(a => a.Barn).Distinct().Should().HaveCount(2,
                $"both barns should be scheduled on {date}");
            dayAssignments.Select(a => a.Shift).Distinct().Should().HaveCount(2,
                $"both shifts should be scheduled on {date}");
        }
    }
}
