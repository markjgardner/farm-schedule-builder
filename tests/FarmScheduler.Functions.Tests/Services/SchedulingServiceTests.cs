using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FarmScheduler.Functions.Tests.Services;

public class SchedulingServiceTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);  // Monday
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);   // Sunday (14 days)
    private const int TotalSlots = 56; // 14 days × 2 barns × 2 shifts

    private readonly SchedulingService _sut;

    public SchedulingServiceTests()
    {
        var logger = Mock.Of<ILogger<SchedulingService>>();
        _sut = new SchedulingService(logger, FixedSeed);
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

    #endregion

    [Fact]
    public void BasicSchedule_AllWorkersAvailable()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments.Should().HaveCount(TotalSlots);
        schedule.Assignments.Should().OnlyContain(a => a.WorkerId != "" && a.WorkerName != "UNFILLED");
        schedule.WindowStart.Should().Be(WindowStart);
        schedule.WindowEnd.Should().Be(WindowEnd);
    }

    [Fact]
    public void FairnessDistribution()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        var counts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();

        // Each worker should get ~14 shifts (56/4). Allow ±2 tolerance.
        counts.Should().HaveCount(4);
        counts.Should().OnlyContain(c => c >= 12 && c <= 16);
    }

    [Fact]
    public void RespectsNotAvailable()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        // Mark worker 1 as NotAvailable on day 1
        var targetDate = WindowStart;
        foreach (var a in avail.Where(a => a.WorkerId == "w1" && a.Date == targetDate))
        {
            a.Status = AvailabilityStatus.NotAvailable;
        }

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments
            .Where(a => a.Date == targetDate && a.WorkerId == "w1")
            .Should().BeEmpty();
    }

    [Fact]
    public void RespectsMorningOnly()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var targetDate = WindowStart;
        foreach (var a in avail.Where(a => a.WorkerId == "w1" && a.Date == targetDate))
        {
            a.Status = AvailabilityStatus.MorningOnly;
        }

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments
            .Where(a => a.Date == targetDate && a.WorkerId == "w1" && a.Shift == ShiftTime.Evening)
            .Should().BeEmpty();
    }

    [Fact]
    public void RespectsEveningOnly()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var targetDate = WindowStart;
        foreach (var a in avail.Where(a => a.WorkerId == "w1" && a.Date == targetDate))
        {
            a.Status = AvailabilityStatus.EveningOnly;
        }

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments
            .Where(a => a.Date == targetDate && a.WorkerId == "w1" && a.Shift == ShiftTime.Morning)
            .Should().BeEmpty();
    }

    [Fact]
    public void NoConflicts_SameTimeslot()
    {
        var workers = CreateWorkers(4);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        // No worker should be assigned to both barns for the same (date, shift)
        var conflicts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.Date, a.Shift, a.WorkerId))
            .Where(g => g.Count() > 1)
            .ToList();

        conflicts.Should().BeEmpty("no worker should be assigned to both barns at the same time");
    }

    [Fact]
    public void ShiftClustering()
    {
        // With 3 workers and 4 slots per day, pigeonhole guarantees at least 1 worker
        // per day must work 2 shifts. The clustering bonus reinforces this pattern.
        var workers = CreateWorkers(3);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        var workerDayCounts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.WorkerId, a.Date))
            .Select(g => g.Count())
            .ToList();

        int clusteredPairs = workerDayCounts.Count(c => c >= 2);

        // With 3 workers and 4 slots/day, at least 1 worker per day must double up
        clusteredPairs.Should().BeGreaterThanOrEqualTo(14,
            "with 3 workers and 4 slots per day, at least one worker per day must have 2 shifts");
    }

    [Fact]
    public void TooFewWorkers()
    {
        var workers = CreateWorkers(1);
        var avail = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments.Should().HaveCount(TotalSlots);

        // 1 worker can do max 2 shifts per day (morning at one barn, evening at one barn — or 1 per timeslot)
        // Actually: 2 timeslots per day, 1 worker per timeslot = max 2 per day
        var filledPerDay = schedule.Assignments
            .Where(a => a.WorkerId == "w1")
            .GroupBy(a => a.Date)
            .Select(g => g.Count())
            .ToList();

        filledPerDay.Should().OnlyContain(c => c <= 2, "a single worker can only fill one slot per timeslot");

        int filled = schedule.Assignments.Count(a => a.WorkerId != "");
        int unfilled = schedule.Assignments.Count(a => a.WorkerId == "");
        filled.Should().Be(28, "1 worker × 14 days × 2 timeslots = 28 max");
        unfilled.Should().Be(28);
    }

    [Fact]
    public void NoWorkers()
    {
        var workers = new List<Worker>();
        var avail = new List<Availability>();

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments.Should().HaveCount(TotalSlots);
        schedule.Assignments.Should().OnlyContain(a => a.WorkerId == "" && a.WorkerName == "UNFILLED");
    }

    [Fact]
    public void DefaultAvailability()
    {
        // Workers with no availability records should be treated as Available
        var workers = CreateWorkers(4);
        var avail = new List<Availability>(); // no records at all

        var schedule = _sut.GenerateSchedule(workers, avail, WindowStart, WindowEnd);

        schedule.Assignments.Should().HaveCount(TotalSlots);
        schedule.Assignments.Should().OnlyContain(a => a.WorkerId != "" && a.WorkerName != "UNFILLED");
    }
}
