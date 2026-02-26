using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static FarmScheduler.Integration.Tests.Helpers.ScheduleAssertionHelpers;

namespace FarmScheduler.Integration.Tests;

public class ScheduleDeterminismTests
{
    private static readonly DateOnly WindowStart = new(2025, 1, 6);
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);

    private static string NormalizeJson(Schedule schedule)
    {
        // Normalize GeneratedAt so it doesn't affect comparison
        schedule.GeneratedAt = DateTime.UnixEpoch;
        return ScheduleToJson(schedule);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalSchedule()
    {
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var service1 = new SchedulingService(Mock.Of<ILogger<SchedulingService>>(), 42);
        var schedule1 = service1.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        var service2 = new SchedulingService(Mock.Of<ILogger<SchedulingService>>(), 42);
        var schedule2 = service2.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        var json1 = NormalizeJson(schedule1);
        var json2 = NormalizeJson(schedule2);

        json1.Should().Be(json2, "the same seed should produce identical schedules");
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSchedules()
    {
        var workers = CreateWorkers(6);
        var availability = CreateAllAvailable(workers, WindowStart, WindowEnd);

        var service42 = new SchedulingService(Mock.Of<ILogger<SchedulingService>>(), 42);
        var schedule42 = service42.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        var service99 = new SchedulingService(Mock.Of<ILogger<SchedulingService>>(), 99);
        var schedule99 = service99.GenerateSchedule(workers, availability, WindowStart, WindowEnd);

        var differs = schedule42.Assignments
            .Zip(schedule99.Assignments)
            .Any(pair => pair.First.WorkerId != pair.Second.WorkerId);

        differs.Should().BeTrue("different seeds should produce at least one different worker assignment");
    }
}
