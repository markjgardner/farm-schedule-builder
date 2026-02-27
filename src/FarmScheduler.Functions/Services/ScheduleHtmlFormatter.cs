using System.Text;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Services;

public static class ScheduleHtmlFormatter
{
    private static readonly (Barn Barn, ShiftTime Shift, string Label)[] Columns =
    {
        (Barn.Windhover, ShiftTime.Morning, "Windhover Morning"),
        (Barn.Windhover, ShiftTime.Evening, "Windhover Evening"),
        (Barn.York, ShiftTime.Morning, "York Morning"),
        (Barn.York, ShiftTime.Evening, "York Evening"),
    };

    public static string ToHtml(Schedule schedule)
    {
        var dates = schedule.Assignments
            .Select(a => a.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var lookup = schedule.Assignments
            .ToLookup(a => (a.Date, a.Barn, a.Shift));

        var sb = new StringBuilder();
        sb.AppendLine("<html><head><style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("h2 { color: #333; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }");
        sb.AppendLine("th { background-color: #4a7c59; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine(".unfilled { color: #c0392b; font-weight: bold; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h2>Farm Schedule: {schedule.WindowStart:yyyy-MM-dd} &ndash; {schedule.WindowEnd:yyyy-MM-dd}</h2>");
        sb.AppendLine($"<p>Generated: {schedule.GeneratedAt:yyyy-MM-dd HH:mm} UTC</p>");

        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>Date</th>");
        foreach (var col in Columns)
            sb.AppendLine($"<th>{col.Label}</th>");
        sb.AppendLine("</tr></thead>");

        sb.AppendLine("<tbody>");
        foreach (var date in dates)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{date:yyyy-MM-dd}</td>");
            foreach (var col in Columns)
            {
                var assignments = lookup[(date, col.Barn, col.Shift)].ToList();
                if (assignments.Count == 0)
                {
                    sb.AppendLine("<td class=\"unfilled\">UNFILLED</td>");
                }
                else
                {
                    var names = string.Join(", ",
                        assignments.Select(a =>
                            string.IsNullOrEmpty(a.WorkerId)
                                ? $"<span class=\"unfilled\">{a.WorkerName}</span>"
                                : a.WorkerName));
                    sb.AppendLine($"<td>{names}</td>");
                }
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
