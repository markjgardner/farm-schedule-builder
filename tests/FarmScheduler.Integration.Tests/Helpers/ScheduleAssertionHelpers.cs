using System.Text.Json;
using System.Text.Json.Serialization;
using FarmScheduler.Core.Models;
using FluentAssertions;

namespace FarmScheduler.Integration.Tests.Helpers;

/// <summary>
/// Shared assertion helpers for scheduling integration tests.
/// </summary>
public static class ScheduleAssertionHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public static List<Worker> CreateWorkers(int count)
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

    public static List<Availability> CreateAllAvailable(List<Worker> workers, DateOnly start, DateOnly end)
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

    public static void SetWorkerAvailability(
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

    public static void AssertNoConflicts(Schedule schedule)
    {
        var conflicts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.Date, a.Shift, a.WorkerId))
            .Where(g => g.Count() > 1)
            .ToList();

        conflicts.Should().BeEmpty("no worker should be assigned to both barns at the same time");
    }

    public static void AssertAvailabilityRespected(Schedule schedule, List<Availability> availability)
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

    public static void AssertAllSlotsPresent(Schedule schedule, int expectedDays)
    {
        int expectedSlots = expectedDays * 4; // 2 barns × 2 shifts
        schedule.Assignments.Should().HaveCount(expectedSlots);

        foreach (var assignment in schedule.Assignments)
        {
            (assignment.WorkerId != "" || assignment.WorkerName == "UNFILLED")
                .Should().BeTrue("every slot must be filled or marked UNFILLED");
        }
    }

    public static void AssertReasonableFairness(Schedule schedule, int workerCount)
    {
        var counts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId)
            .Select(g => g.Count())
            .ToList();

        if (counts.Count == 0) return;

        int min = counts.Min();
        int max = counts.Max();

        max.Should().BeLessThanOrEqualTo((int)(min * 1.5) + 1,
            $"fairness: max {max} shifts vs min {min} shifts exceeds 50% threshold");
    }

    /// <summary>
    /// Returns the percentage of worked days where a worker has 2+ shifts (clustering).
    /// A higher rate means better clustering.
    /// </summary>
    public static double GetClusteringRate(Schedule schedule)
    {
        var workerDayShifts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.WorkerId, a.Date))
            .ToList();

        if (workerDayShifts.Count == 0) return 0;

        int clusteredDays = workerDayShifts.Count(g => g.Count() >= 2);
        return (double)clusteredDays / workerDayShifts.Count;
    }

    public static void AssertClusteringRate(Schedule schedule, double minRate)
    {
        double rate = GetClusteringRate(schedule);
        rate.Should().BeGreaterThanOrEqualTo(minRate,
            $"clustering rate {rate:P0} should be at least {minRate:P0}");
    }

    /// <summary>
    /// Asserts no worker is assigned to one barn more than maxSkew fraction of the time.
    /// </summary>
    public static void AssertBarnBalance(Schedule schedule, double maxSkew)
    {
        var workerAssignments = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => a.WorkerId);

        foreach (var workerGroup in workerAssignments)
        {
            int total = workerGroup.Count();
            if (total < 4) continue; // skip workers with very few shifts

            var barnCounts = workerGroup.GroupBy(a => a.Barn).Select(g => g.Count()).ToList();
            int maxBarnCount = barnCounts.Max();
            double barnRatio = (double)maxBarnCount / total;

            barnRatio.Should().BeLessThanOrEqualTo(maxSkew,
                $"Worker {workerGroup.Key} has {barnRatio:P0} of shifts at one barn (max allowed: {maxSkew:P0})");
        }
    }

    public static void AssertUnfilledCount(Schedule schedule, int expectedCount)
    {
        int unfilled = schedule.Assignments.Count(a => a.WorkerName == "UNFILLED");
        unfilled.Should().Be(expectedCount);
    }

    public static void AssertUnfilledAtLeast(Schedule schedule, int minCount)
    {
        int unfilled = schedule.Assignments.Count(a => a.WorkerName == "UNFILLED");
        unfilled.Should().BeGreaterThanOrEqualTo(minCount,
            $"expected at least {minCount} unfilled slots but got {unfilled}");
    }

    public static void AssertNoUnfilled(Schedule schedule)
    {
        schedule.Assignments.Where(a => a.WorkerName == "UNFILLED").Should().BeEmpty(
            "all slots should be filled");
    }

    /// <summary>
    /// Counts the number of (worker, day) pairs where the worker works exactly 1 shift.
    /// </summary>
    public static int CountSingleShiftDays(Schedule schedule)
    {
        return schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.WorkerId, a.Date))
            .Count(g => g.Count() == 1);
    }

    public static string ScheduleToJson(Schedule schedule)
    {
        return JsonSerializer.Serialize(schedule, JsonOptions);
    }
}
