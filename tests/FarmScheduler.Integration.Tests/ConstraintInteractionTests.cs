using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class ConstraintInteractionTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);
    private const int DaysInWindow = 14;
    private const int TotalSlots = DaysInWindow * 4; // 56

    private readonly SchedulingService _schedulingService;

    public ConstraintInteractionTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void MorningOnlyAndEveningOnlyWorkers_SameDay_BothAssigned()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // w1: MorningOnly every day
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w1", date, AvailabilityStatus.MorningOnly);

        // w2: EveningOnly every day
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w2", date, AvailabilityStatus.EveningOnly);

        // w3 and w4: fully available (default)

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        var w1Assignments = schedule.Assignments.Where(a => a.WorkerId == "w1").ToList();
        var w2Assignments = schedule.Assignments.Where(a => a.WorkerId == "w2").ToList();

        w1Assignments.Should().OnlyContain(a => a.Shift == ShiftTime.Morning,
            "w1 is MorningOnly and should only have Morning shifts");
        w2Assignments.Should().OnlyContain(a => a.Shift == ShiftTime.Evening,
            "w2 is EveningOnly and should only have Evening shifts");

        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);
    }

    [Fact]
    public void BarnAssignmentIsScoringDriven_NotWorkerDriven()
    {
        // Use 6 workers so the fairness scoring forces rotation across barns
        var workers = CreateWorkers(6);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Count how many workers are assigned to both barns
        int workersWithBothBarns = workers.Count(worker =>
        {
            var barns = schedule.Assignments
                .Where(a => a.WorkerId == worker.Id)
                .Select(a => a.Barn)
                .Distinct()
                .ToList();
            return barns.Contains(Barn.Windhover) && barns.Contains(Barn.York);
        });

        // With 6 workers competing for 4 daily slots, fairness scoring rotates workers
        // across barns — the algorithm doesn't hard-lock workers to a single barn.
        workersWithBothBarns.Should().BeGreaterThanOrEqualTo(1,
            "barn assignment is algorithmic — at least one worker should be assigned to both barns");

        // Both barns must appear in the overall schedule
        var allBarns = schedule.Assignments.Select(a => a.Barn).Distinct().ToList();
        allBarns.Should().Contain(Barn.Windhover);
        allBarns.Should().Contain(Barn.York);
    }

    [Fact]
    public void OnlyFinalAvailabilityMatters_OverwrittenPreference()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var targetDate = new DateOnly(2025, 1, 6);

        // First mark w1 as NotAvailable on Jan 6
        SetWorkerAvailability(avail, "w1", targetDate, AvailabilityStatus.NotAvailable);

        // Overwrite to Available
        SetWorkerAvailability(avail, "w1", targetDate, AvailabilityStatus.Available);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        var w1OnTargetDate = schedule.Assignments
            .Where(a => a.WorkerId == "w1" && a.Date == targetDate)
            .ToList();

        w1OnTargetDate.Should().NotBeEmpty(
            "w1's NotAvailable was overwritten to Available, so w1 should be scheduled on Jan 6");
    }
}
