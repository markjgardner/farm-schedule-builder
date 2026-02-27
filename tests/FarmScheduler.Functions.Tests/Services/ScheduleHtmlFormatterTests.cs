using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using FluentAssertions;

namespace FarmScheduler.Functions.Tests.Services;

public class ScheduleHtmlFormatterTests
{
    [Fact]
    public void ToHtml_ReturnsValidHtmlTable()
    {
        var schedule = new Schedule
        {
            WindowStart = new DateOnly(2025, 3, 3),
            WindowEnd = new DateOnly(2025, 3, 16),
            GeneratedAt = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            Assignments = new List<ShiftAssignment>
            {
                new() { Date = new DateOnly(2025, 3, 3), Barn = Barn.Windhover, Shift = ShiftTime.Morning, WorkerId = "w1", WorkerName = "Alice" },
                new() { Date = new DateOnly(2025, 3, 3), Barn = Barn.Windhover, Shift = ShiftTime.Evening, WorkerId = "w2", WorkerName = "Bob" },
                new() { Date = new DateOnly(2025, 3, 3), Barn = Barn.York, Shift = ShiftTime.Morning, WorkerId = "", WorkerName = "UNFILLED" },
                new() { Date = new DateOnly(2025, 3, 3), Barn = Barn.York, Shift = ShiftTime.Evening, WorkerId = "w1", WorkerName = "Alice" },
            }
        };

        var html = ScheduleHtmlFormatter.ToHtml(schedule);

        html.Should().Contain("<html>");
        html.Should().Contain("<table>");
        html.Should().Contain("2025-03-03");
        html.Should().Contain("Alice");
        html.Should().Contain("Bob");
        html.Should().Contain("UNFILLED");
        html.Should().Contain("class=\"unfilled\"");
        html.Should().Contain("Windhover Morning");
        html.Should().Contain("York Evening");
        html.Should().Contain("2025-03-03 &ndash; 2025-03-16");
    }

    [Fact]
    public void ToHtml_EmptySchedule_ReturnsTableWithNoRows()
    {
        var schedule = new Schedule
        {
            WindowStart = new DateOnly(2025, 3, 3),
            WindowEnd = new DateOnly(2025, 3, 16),
            Assignments = new List<ShiftAssignment>()
        };

        var html = ScheduleHtmlFormatter.ToHtml(schedule);

        html.Should().Contain("<table>");
        html.Should().Contain("<thead>");
        html.Should().NotContain("<td>2025");
    }
}
