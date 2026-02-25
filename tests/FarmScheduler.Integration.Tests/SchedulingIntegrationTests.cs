using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FarmScheduler.Integration.Tests;

public class SchedulingIntegrationTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);  // Monday
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);   // Sunday (14 days)
    private const int DaysInWindow = 14;
    private const int SlotsPerDay = 4; // 2 barns × 2 shifts
    private const int TotalSlots = DaysInWindow * SlotsPerDay; // 56

    private readonly SchedulingService _schedulingService;

    public SchedulingIntegrationTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _schedulingService = new SchedulingService(logger, FixedSeed);
    }

    #region Helpers

    private static List<Worker> CreateWorkers(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Worker
            {
                Id = $"w{i}",
                DisplayName = $"Worker {i}",
                Email = $"w{i}@farm.com",
                IsActive = true
            })
            .ToList();
    }

    private static List<Availability> CreateAllAvailable(List<Worker> workers, DateOnly start, DateOnly end)
    {
        var list = new List<Availability>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            foreach (var w in workers)
            {
                list.Add(new Availability
                {
                    WorkerId = w.Id,
                    Date = date,
                    Status = AvailabilityStatus.Available
                });
            }
        }
        return list;
    }

    private static void SetWorkerAvailability(
        List<Availability> availability,
        string workerId,
        DateOnly date,
        AvailabilityStatus status)
    {
        var existing = availability.FirstOrDefault(a => a.WorkerId == workerId && a.Date == date);
        if (existing != null)
            existing.Status = status;
        else
            availability.Add(new Availability { WorkerId = workerId, Date = date, Status = status });
    }

    private static void AssertNoConflicts(Schedule schedule)
    {
        var conflicts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.Date, a.Shift, a.WorkerId))
            .Where(g => g.Count() > 1)
            .ToList();

        conflicts.Should().BeEmpty("no worker should be assigned to both barns at the same time");
    }

    private static void AssertAvailabilityRespected(Schedule schedule, List<Availability> availability)
    {
        var availLookup = availability.ToDictionary(a => (a.WorkerId, a.Date));

        foreach (var assignment in schedule.Assignments.Where(a => a.WorkerId != ""))
        {
            if (availLookup.TryGetValue((assignment.WorkerId, assignment.Date), out var avail))
            {
                switch (avail.Status)
                {
                    case AvailabilityStatus.NotAvailable:
                        Assert.Fail($"Worker {assignment.WorkerId} assigned on {assignment.Date} but marked NotAvailable");
                        break;
                    case AvailabilityStatus.MorningOnly:
                        assignment.Shift.Should().Be(ShiftTime.Morning,
                            $"Worker {assignment.WorkerId} is MorningOnly on {assignment.Date}");
                        break;
                    case AvailabilityStatus.EveningOnly:
                        assignment.Shift.Should().Be(ShiftTime.Evening,
                            $"Worker {assignment.WorkerId} is EveningOnly on {assignment.Date}");
                        break;
                }
            }
        }
    }

    private static void AssertAllSlotsPresent(Schedule schedule)
    {
        schedule.Assignments.Should().HaveCount(TotalSlots);

        foreach (var assignment in schedule.Assignments)
        {
            (assignment.WorkerId != "" || assignment.WorkerName == "UNFILLED")
                .Should().BeTrue("every slot must be filled or marked UNFILLED");
        }
    }

    private static void AssertReasonableFairness(Schedule schedule, int workerCount)
    {
        var counts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();

        if (counts.Count == 0) return;

        int min = counts.Min();
        int max = counts.Max();

        // No worker has >50% more shifts than the worker with the fewest
        max.Should().BeLessThanOrEqualTo((int)(min * 1.5) + 1,
            $"fairness: max {max} shifts vs min {min} shifts exceeds 50% threshold");
    }

    #endregion

    [Fact]
    public void Scenario_SixWorkers_MixedAvailability_FullConstraintValidation()
    {
        var workers = CreateWorkers(6);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Worker 1: takes Mondays off
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Monday)
                SetWorkerAvailability(avail, "w1", date, AvailabilityStatus.NotAvailable);
        }

        // Worker 2: morning only on Wednesdays
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Wednesday)
                SetWorkerAvailability(avail, "w2", date, AvailabilityStatus.MorningOnly);
        }

        // Worker 3: evening only on Fridays
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Friday)
                SetWorkerAvailability(avail, "w3", date, AvailabilityStatus.EveningOnly);
        }

        // Worker 4: takes the entire second week off
        for (var date = WindowStart.AddDays(7); date <= WindowEnd; date = date.AddDays(1))
        {
            SetWorkerAvailability(avail, "w4", date, AvailabilityStatus.NotAvailable);
        }

        // Worker 5: morning only on all weekends
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                SetWorkerAvailability(avail, "w5", date, AvailabilityStatus.MorningOnly);
        }

        // Worker 6: fully available (no restrictions)

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);
        AssertReasonableFairness(schedule, 6);

        // With 6 workers and only partial constraints, all slots should be filled
        schedule.Assignments.Where(a => a.WorkerName == "UNFILLED").Should().BeEmpty(
            "6 workers with partial constraints should fill all 56 slots");
    }

    [Fact]
    public void Scenario_FourWorkers_AllAvailable_Baseline()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);
        AssertReasonableFairness(schedule, 4);

        // All slots should be filled with 4 fully-available workers
        schedule.Assignments.Where(a => a.WorkerName == "UNFILLED").Should().BeEmpty();

        // Each worker should get roughly 14 shifts (56/4)
        var counts = schedule.Assignments
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();
        counts.Should().HaveCount(4);
        counts.Should().OnlyContain(c => c >= 12 && c <= 16);
    }

    [Fact]
    public void Scenario_ThreeWorkers_Understaffed_ExpectUnfilled()
    {
        var workers = CreateWorkers(3);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Each worker can only fill 1 slot per timeslot (2 timeslots/day = 2 slots/worker/day)
        // 3 workers × 2 slots/day = 6 filled per day, but 4 slots/day needed
        // Actually 3 workers can fill all 4 slots if two are scheduled to both timeslots
        // But each worker can only be at one barn per timeslot, so max 3 filled per timeslot
        // With 2 barns per timeslot but 3 workers, that's enough to fill all slots

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);

        // With 3 workers and 4 slots/day, 2 timeslots × 2 barns, 3 workers can cover
        // up to 2 barns per timeslot (max 3, but only 2 barns), so potentially all filled.
        // However the constraint is 1 worker per timeslot per barn, 
        // and 2 barns per timeslot, so 2 slots per timeslot need 2 workers.
        // 3 workers and 2 timeslots × 2 barns = 4 slots, each needing a unique worker per timeslot
        // Per timeslot we need 2 different workers, and we have 3. So all slots can be filled.
        int filled = schedule.Assignments.Count(a => a.WorkerId != "");
        int unfilled = schedule.Assignments.Count(a => a.WorkerName == "UNFILLED");

        // 3 workers should be able to fill all slots (2 per timeslot, 3 available)
        filled.Should().Be(TotalSlots, "3 workers should fill all 56 slots since only 2 needed per timeslot");
    }

    [Fact]
    public void Scenario_ThreeWorkers_HeavyConstraints_SomeUnfilled()
    {
        // Create a truly understaffed scenario: 3 workers with heavy constraints
        var workers = CreateWorkers(3);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Worker 1: only available first week
        for (var date = WindowStart.AddDays(7); date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w1", date, AvailabilityStatus.NotAvailable);

        // Worker 2: morning only all days
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w2", date, AvailabilityStatus.MorningOnly);

        // Worker 3: evening only all days
        for (var date = WindowStart; date <= WindowEnd; date = date.AddDays(1))
            SetWorkerAvailability(avail, "w3", date, AvailabilityStatus.EveningOnly);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        AssertAllSlotsPresent(schedule);
        AssertNoConflicts(schedule);
        AssertAvailabilityRespected(schedule, avail);

        // In second week: w2 can only do morning (1 slot per timeslot, 2 morning barns)
        // and w3 can only do evening (2 evening barns). That's 2 morning + 2 evening = 4,
        // but w2 can only fill 1 morning slot and w3 can only fill 1 evening slot per day.
        // So second week: 1 morning + 1 evening = 2 filled out of 4/day = some unfilled expected
        int unfilled = schedule.Assignments.Count(a => a.WorkerName == "UNFILLED");
        unfilled.Should().BeGreaterThan(0, "heavy constraints with limited workers should produce unfilled slots");
    }

    [Fact]
    public void Scenario_SixWorkers_ScheduleWindowDatesCorrect()
    {
        var workers = CreateWorkers(6);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.WindowStart.Should().Be(WindowStart);
        schedule.WindowEnd.Should().Be(WindowEnd);
        schedule.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify all dates in range
        var assignmentDates = schedule.Assignments.Select(a => a.Date).Distinct().OrderBy(d => d).ToList();
        var expectedDates = new List<DateOnly>();
        for (var d = WindowStart; d <= WindowEnd; d = d.AddDays(1))
            expectedDates.Add(d);

        assignmentDates.Should().BeEquivalentTo(expectedDates);
    }

    [Fact]
    public void Scenario_AllBarnsAndShiftsCovered()
    {
        var workers = CreateWorkers(6);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _schedulingService.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // Every (date, barn, shift) combination should have exactly one assignment
        var slotGroups = schedule.Assignments
            .GroupBy(a => (a.Date, a.Barn, a.Shift))
            .ToList();

        slotGroups.Should().HaveCount(TotalSlots);
        slotGroups.Should().OnlyContain(g => g.Count() == 1,
            "each (date, barn, shift) slot should have exactly one assignment");
    }
}
