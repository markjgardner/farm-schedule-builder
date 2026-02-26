using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class HighContentionTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);  // Monday
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);   // Sunday (14 days)
    private const int DaysInWindow = 14;
    private const int SlotsPerDay = 4; // 2 barns × 2 shifts

    private readonly SchedulingService _schedulingService;

    public HighContentionTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    [Fact]
    public void FourWorkers_EachAvailableThreeDays_OverlappingWindows()
    {
        // Arrange: 4 workers with narrow, overlapping availability windows
        var workers = CreateWorkers(4);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Set all days to NotAvailable first, then open small windows
        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
        {
            foreach (var w in workers)
                SetWorkerAvailability(availability, w.Id, d, AvailabilityStatus.NotAvailable);
        }

        // w1: Jan 6-8, w2: Jan 8-10, w3: Jan 10-12, w4: Jan 12-14
        for (var d = new DateOnly(2025, 1, 6); d <= new DateOnly(2025, 1, 8); d = d.AddDays(1))
            SetWorkerAvailability(availability, workers[0].Id, d, AvailabilityStatus.Available);
        for (var d = new DateOnly(2025, 1, 8); d <= new DateOnly(2025, 1, 10); d = d.AddDays(1))
            SetWorkerAvailability(availability, workers[1].Id, d, AvailabilityStatus.Available);
        for (var d = new DateOnly(2025, 1, 10); d <= new DateOnly(2025, 1, 12); d = d.AddDays(1))
            SetWorkerAvailability(availability, workers[2].Id, d, AvailabilityStatus.Available);
        for (var d = new DateOnly(2025, 1, 12); d <= new DateOnly(2025, 1, 14); d = d.AddDays(1))
            SetWorkerAvailability(availability, workers[3].Id, d, AvailabilityStatus.Available);

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: many days have 0-1 available workers for 4 slots
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, availability);
        AssertUnfilledAtLeast(schedule, 1);
    }

    [Fact]
    public void MutuallyExclusiveAvailability_NoOverlap()
    {
        // Arrange: 2 workers with no overlapping days
        var workers = CreateWorkers(2);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
        {
            foreach (var w in workers)
                SetWorkerAvailability(availability, w.Id, d, AvailabilityStatus.NotAvailable);
        }

        // w1: Mon/Wed/Fri, w2: Tue/Thu — both off weekends
        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
        {
            var dow = d.DayOfWeek;
            if (dow == DayOfWeek.Monday || dow == DayOfWeek.Wednesday || dow == DayOfWeek.Friday)
                SetWorkerAvailability(availability, workers[0].Id, d, AvailabilityStatus.Available);
            else if (dow == DayOfWeek.Tuesday || dow == DayOfWeek.Thursday)
                SetWorkerAvailability(availability, workers[1].Id, d, AvailabilityStatus.Available);
        }

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: each timeslot has at most 1 worker, so at best 2 of 4 slots filled per day
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, availability);
        AssertUnfilledAtLeast(schedule, 1);
    }

    [Fact]
    public void RotatingSingleWorker_FillsAllFourSlots()
    {
        // Arrange: 4 workers, each day exactly one worker is available (rotating)
        var workers = CreateWorkers(4);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
        {
            foreach (var w in workers)
                SetWorkerAvailability(availability, w.Id, d, AvailabilityStatus.NotAvailable);
        }

        // Rotate: day 0→w1, day 1→w2, day 2→w3, day 3→w4, day 4→w1, etc.
        for (var i = 0; i < DaysInWindow; i++)
        {
            var date = WindowStart.AddDays(i);
            var workerIndex = i % 4;
            SetWorkerAvailability(availability, workers[workerIndex].Id, date, AvailabilityStatus.Available);
        }

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert: single worker per day can fill 1 slot per timeslot (2 timeslots) = 2 filled per day
        // 14 days × 2 filled = 28 filled, 14 days × 2 unfilled = 28 unfilled
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, availability);

        var filled = schedule.Assignments.Count(a => !string.IsNullOrEmpty(a.WorkerId));
        var unfilled = schedule.Assignments.Count(a => string.IsNullOrEmpty(a.WorkerId));

        filled.Should().Be(DaysInWindow * 2, "one worker per day fills 1 slot per timeslot × 2 timeslots");
        unfilled.Should().Be(DaysInWindow * 2, "one worker per day leaves 1 slot unfilled per timeslot × 2 timeslots");
    }

    [Fact]
    public void WeekendBlackout_WeekdaysFilled()
    {
        // Arrange: 6 workers, all available weekdays, all off weekends
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
            {
                foreach (var w in workers)
                    SetWorkerAvailability(availability, w.Id, d, AvailabilityStatus.NotAvailable);
            }
        }

        // Act
        var schedule = _schedulingService.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        // Assert
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, availability);

        // 4 weekend days (2 Sat + 2 Sun) × 4 slots = 16 unfilled
        var weekendSlots = schedule.Assignments
            .Where(a => a.Date.DayOfWeek == DayOfWeek.Saturday || a.Date.DayOfWeek == DayOfWeek.Sunday)
            .ToList();
        weekendSlots.Should().HaveCount(16, "4 weekend days × 4 slots");
        weekendSlots.Should().AllSatisfy(a =>
            a.WorkerId.Should().BeNullOrEmpty("no worker is available on weekends"));

        // 10 weekdays × 4 slots = 40 filled
        var weekdaySlots = schedule.Assignments
            .Where(a => a.Date.DayOfWeek != DayOfWeek.Saturday && a.Date.DayOfWeek != DayOfWeek.Sunday)
            .ToList();
        weekdaySlots.Should().HaveCount(40, "10 weekdays × 4 slots");
        weekdaySlots.Should().AllSatisfy(a =>
            a.WorkerId.Should().NotBeNullOrEmpty("6 workers should fill all weekday slots"));
    }
}
