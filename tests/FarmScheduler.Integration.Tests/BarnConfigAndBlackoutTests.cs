using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FarmScheduler.Integration.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FarmScheduler.Integration.Tests;

public class BarnConfigAndBlackoutTests
{
    private readonly SchedulingService _scheduler;
    private readonly DateOnly _start = new(2024, 1, 15); // Monday
    private readonly DateOnly _end = new(2024, 1, 28);   // Sunday (2 weeks)

    public BarnConfigAndBlackoutTests()
    {
        _scheduler = new SchedulingService(new Mock<ILogger<SchedulingService>>().Object, randomSeed: 42);
    }

    // --- Multi-worker barn config tests ---

    [Fact]
    public void MultiWorkerBarn_GeneratesCorrectSlotCount()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(8);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 },
            new() { Barn = Barn.Windhover, WorkersPerShift = 1 }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs);

        // 14 days × (York: 2 shifts × 2 workers + Windhover: 2 shifts × 1 worker) = 14 × 6 = 84
        schedule.Assignments.Should().HaveCount(84);
    }

    [Fact]
    public void MultiWorkerBarn_NoDuplicateAssignmentsInSameSlot()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(8);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 },
            new() { Barn = Barn.Windhover, WorkersPerShift = 1 }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs);

        // For each (date, barn, shift), no worker should appear twice
        var groups = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.Date, a.Barn, a.Shift));

        foreach (var group in groups)
        {
            var workerIds = group.Select(a => a.WorkerId).ToList();
            workerIds.Should().OnlyHaveUniqueItems(
                $"no worker should be assigned twice to {group.Key}");
        }
    }

    [Fact]
    public void MultiWorkerBarn_StillRespectsConflicts()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(6);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 },
            new() { Barn = Barn.Windhover, WorkersPerShift = 2 }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs);

        ScheduleAssertionHelpers.AssertNoConflicts(schedule);
    }

    [Fact]
    public void MultiWorkerBarn_RespectsAvailability()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(6);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        // Worker 1 unavailable for first 3 days
        for (var d = _start; d < _start.AddDays(3); d = d.AddDays(1))
            ScheduleAssertionHelpers.SetWorkerAvailability(availability, "w1", d, AvailabilityStatus.NotAvailable);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs);

        ScheduleAssertionHelpers.AssertAvailabilityRespected(schedule, availability);
    }

    [Fact]
    public void DefaultBarnConfig_WhenNoneProvided()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(4);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        // No barn configs = default 1 worker per shift
        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end);

        // 14 days × 2 barns × 2 shifts × 1 worker = 56
        schedule.Assignments.Should().HaveCount(56);
    }

    // --- Blackout date tests ---

    [Fact]
    public void WholeDayBlackout_SkipsAllShifts()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(4);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20", Date = new DateOnly(2024, 1, 20), Description = "Holiday" }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, blackouts: blackouts);

        // 13 days × 4 slots + 0 (blacked out day) = 52
        schedule.Assignments.Should().HaveCount(52);
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 20));
    }

    [Fact]
    public void BarnSpecificBlackout_SkipsOnlyThatBarn()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(4);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20_York", Date = new DateOnly(2024, 1, 20), Barn = Barn.York }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, blackouts: blackouts);

        // 14 × 4 - 2 (York morning + evening) = 54
        schedule.Assignments.Should().HaveCount(54);
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.York);
        schedule.Assignments.Should().Contain(a => a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.Windhover);
    }

    [Fact]
    public void ShiftSpecificBlackout_SkipsOnlyThatShift()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(4);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20_York_Morning", Date = new DateOnly(2024, 1, 20), Barn = Barn.York, Shift = ShiftTime.Morning }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, blackouts: blackouts);

        // 56 - 1 = 55
        schedule.Assignments.Should().HaveCount(55);
        schedule.Assignments.Should().NotContain(a =>
            a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.York && a.Shift == ShiftTime.Morning);
        // Other slots on that day still exist
        schedule.Assignments.Should().Contain(a =>
            a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.York && a.Shift == ShiftTime.Evening);
    }

    [Fact]
    public void MultipleBlackouts_AllRespected()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(4);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20", Date = new DateOnly(2024, 1, 20), Description = "Holiday 1" },
            new() { Id = "2024-01-25", Date = new DateOnly(2024, 1, 25), Description = "Holiday 2" }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, blackouts: blackouts);

        // 12 days × 4 = 48
        schedule.Assignments.Should().HaveCount(48);
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 20));
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 25));
    }

    // --- Combined tests ---

    [Fact]
    public void MultiWorkerBarn_WithBlackouts()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(8);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 },
            new() { Barn = Barn.Windhover, WorkersPerShift = 1 }
        };

        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20", Date = new DateOnly(2024, 1, 20), Description = "Holiday" }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs, blackouts);

        // 13 active days × (York: 2×2 + Windhover: 1×2) = 13 × 6 = 78
        schedule.Assignments.Should().HaveCount(78);
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 20));
        ScheduleAssertionHelpers.AssertNoConflicts(schedule);
    }

    [Fact]
    public void BarnBlackout_WithMultiWorkerConfig()
    {
        var workers = ScheduleAssertionHelpers.CreateWorkers(6);
        var availability = ScheduleAssertionHelpers.CreateAllAvailable(workers, _start, _end);

        var barnConfigs = new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 },
            new() { Barn = Barn.Windhover, WorkersPerShift = 1 }
        };

        // Black out only York on one day
        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-01-20_York", Date = new DateOnly(2024, 1, 20), Barn = Barn.York }
        };

        var schedule = _scheduler.GenerateSchedule(workers, availability, _start, _end, barnConfigs, blackouts);

        // Normal: 14 × 6 = 84. Minus York on Jan 20 (2 shifts × 2 workers = 4): 80
        schedule.Assignments.Should().HaveCount(80);
        schedule.Assignments.Should().NotContain(a => a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.York);
        schedule.Assignments.Should().Contain(a => a.Date == new DateOnly(2024, 1, 20) && a.Barn == Barn.Windhover);
    }
}
