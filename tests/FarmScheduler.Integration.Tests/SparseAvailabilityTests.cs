using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class SparseAvailabilityTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);  // Monday
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);   // Sunday (14 days)
    private const int DaysInWindow = 14;
    private const int SlotsPerDay = 4; // 2 barns × 2 shifts
    private const int TotalSlots = DaysInWindow * SlotsPerDay; // 56

    private readonly SchedulingService _schedulingService;

    public SparseAvailabilityTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void HalfWorkersSubmitNoAvailability_DefaultToAvailable()
    {
        // 6 workers, only w1-w3 have explicit availability records (all Available).
        // w4-w6 have NO availability records at all → should default to Available.
        var workers = CreateWorkers(6);
        var avail = CreateAllAvailable(workers.GetRange(0, 3), WindowStart, WindowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertNoUnfilled(schedule);
    }

    [Fact]
    public void PartialDayCoverage_UnrecordedDaysDefaultAvailable()
    {
        // 4 workers, only submit availability for the first 7 days.
        // The remaining 7 days have no records → default Available.
        var workers = CreateWorkers(4);
        var firstWeekEnd = WindowStart.AddDays(6); // Jan 6–12
        var avail = CreateAllAvailable(workers, WindowStart, firstWeekEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule, DaysInWindow);
        schedule.Assignments.Should().HaveCount(TotalSlots);
        AssertNoUnfilled(schedule);
    }

    [Fact]
    public void AllWorkersUnavailableSameDay_SlotsUnfilled()
    {
        // 4 workers, all available except ALL are NotAvailable on Jan 10.
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var blackoutDate = new DateOnly(2025, 1, 10);
        foreach (var w in workers)
        {
            SetWorkerAvailability(avail, w.Id, blackoutDate, AvailabilityStatus.NotAvailable);
        }

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);

        // The 4 slots on Jan 10 should all be UNFILLED
        var blackoutSlots = schedule.Assignments.Where(a => a.Date == blackoutDate).ToList();
        blackoutSlots.Should().HaveCount(SlotsPerDay);
        blackoutSlots.Should().OnlyContain(a => a.WorkerName == "UNFILLED",
            "all slots on the blackout date should be UNFILLED");

        // Other days should be filled
        var otherSlots = schedule.Assignments.Where(a => a.Date != blackoutDate).ToList();
        otherSlots.Should().OnlyContain(a => a.WorkerId != "",
            "all slots on non-blackout days should be filled");
    }

    [Fact]
    public void AllWorkersMorningOnly_EveningSlotsUnfilled()
    {
        // 4 workers, all marked MorningOnly for every day.
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            foreach (var w in workers)
            {
                SetWorkerAvailability(avail, w.Id, date, AvailabilityStatus.MorningOnly);
            }
        }

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);

        // All 28 evening slots should be UNFILLED
        var eveningSlots = schedule.Assignments.Where(a => a.Shift == ShiftTime.Evening).ToList();
        eveningSlots.Should().HaveCount(DaysInWindow * 2); // 14 days × 2 barns
        eveningSlots.Should().OnlyContain(a => a.WorkerName == "UNFILLED",
            "all evening slots should be UNFILLED when every worker is MorningOnly");

        // All 28 morning slots should be filled
        var morningSlots = schedule.Assignments.Where(a => a.Shift == ShiftTime.Morning).ToList();
        morningSlots.Should().HaveCount(DaysInWindow * 2);
        morningSlots.Should().OnlyContain(a => a.WorkerId != "",
            "all morning slots should be filled");
    }

    [Fact]
    public void AllWorkersEveningOnly_MorningSlotsUnfilled()
    {
        // 4 workers, all marked EveningOnly for every day.
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            foreach (var w in workers)
            {
                SetWorkerAvailability(avail, w.Id, date, AvailabilityStatus.EveningOnly);
            }
        }

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule, DaysInWindow);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);

        // All 28 morning slots should be UNFILLED
        var morningSlots = schedule.Assignments.Where(a => a.Shift == ShiftTime.Morning).ToList();
        morningSlots.Should().HaveCount(DaysInWindow * 2); // 14 days × 2 barns
        morningSlots.Should().OnlyContain(a => a.WorkerName == "UNFILLED",
            "all morning slots should be UNFILLED when every worker is EveningOnly");

        // All 28 evening slots should be filled
        var eveningSlots = schedule.Assignments.Where(a => a.Shift == ShiftTime.Evening).ToList();
        eveningSlots.Should().HaveCount(DaysInWindow * 2);
        eveningSlots.Should().OnlyContain(a => a.WorkerId != "",
            "all evening slots should be filled");
    }
}
