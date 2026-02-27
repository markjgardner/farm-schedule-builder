using FarmScheduler.Core.Models;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Services;

public class SchedulingService : ISchedulingService
{
    private readonly ILogger<SchedulingService> _logger;
    private readonly Random _random;

    public SchedulingService(ILogger<SchedulingService> logger, int? randomSeed = null)
    {
        _logger = logger;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    public Schedule GenerateSchedule(
        IReadOnlyList<Worker> workers,
        IReadOnlyList<Availability> availability,
        DateOnly windowStart,
        DateOnly windowEnd,
        IReadOnlyList<BarnConfig>? barnConfigs = null,
        IReadOnlyList<BlackoutDate>? blackouts = null)
    {
        // Index availability by (WorkerId, Date) for fast lookup
        var availabilityLookup = availability
            .ToDictionary(a => (a.WorkerId, a.Date));

        var activeWorkers = workers.Where(w => w.IsActive).ToList();

        // Build barn staffing lookup (default to 1)
        var staffingLookup = new Dictionary<Barn, int>();
        foreach (var barn in Enum.GetValues<Barn>())
            staffingLookup[barn] = 1;
        if (barnConfigs != null)
        {
            foreach (var cfg in barnConfigs)
                staffingLookup[cfg.Barn] = Math.Max(1, cfg.WorkersPerShift);
        }

        // Build blackout lookup for fast checking
        var blackoutSet = new HashSet<(DateOnly Date, Barn? Barn, ShiftTime? Shift)>();
        if (blackouts != null)
        {
            foreach (var b in blackouts)
                blackoutSet.Add((b.Date, b.Barn, b.Shift));
        }

        // Build all slots, respecting blackouts and staffing levels
        var slots = new List<(DateOnly Date, Barn Barn, ShiftTime Shift)>();
        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
        {
            foreach (var barn in Enum.GetValues<Barn>())
            foreach (var shift in Enum.GetValues<ShiftTime>())
            {
                if (IsBlackedOut(date, barn, shift, blackoutSet))
                    continue;

                int staffingCount = staffingLookup[barn];
                for (int i = 0; i < staffingCount; i++)
                    slots.Add((date, barn, shift));
            }
        }

        // Track assignments per worker
        var workerShiftCount = activeWorkers.ToDictionary(w => w.Id, _ => 0);
        // Track which (day, shift) a worker is already assigned to a barn
        var workerTimeslotBarn = new Dictionary<(string WorkerId, DateOnly Date, ShiftTime Shift), Barn>();
        // Track which days a worker has any assignment
        var workerDayAssigned = new HashSet<(string WorkerId, DateOnly Date)>();
        // Track which barns a worker has been assigned to
        var workerBarnHistory = new HashSet<(string WorkerId, Barn Barn)>();

        var assignments = new List<ShiftAssignment>();

        // Compute eligible counts per slot for constraint ordering
        int CountEligible((DateOnly Date, Barn Barn, ShiftTime Shift) slot)
        {
            return activeWorkers.Count(w => IsEligible(w, slot.Date, slot.Shift, slot.Barn, availabilityLookup, workerTimeslotBarn));
        }

        // Sort slots by most-constrained first (fewest eligible workers)
        var orderedSlots = slots
            .OrderBy(s => CountEligible(s))
            .ThenBy(s => s.Date)
            .ThenBy(s => s.Barn)
            .ThenBy(s => s.Shift)
            .ToList();

        foreach (var slot in orderedSlots)
        {
            var eligible = activeWorkers
                .Where(w => IsEligible(w, slot.Date, slot.Shift, slot.Barn, availabilityLookup, workerTimeslotBarn))
                .ToList();

            if (eligible.Count == 0)
            {
                _logger.LogWarning("No eligible worker for {Date} {Barn} {Shift} — marking UNFILLED",
                    slot.Date, slot.Barn, slot.Shift);
                assignments.Add(new ShiftAssignment
                {
                    Date = slot.Date,
                    Barn = slot.Barn,
                    Shift = slot.Shift,
                    WorkerId = "",
                    WorkerName = "UNFILLED"
                });
                continue;
            }

            int maxShifts = workerShiftCount.Values.DefaultIfEmpty(0).Max();

            var scored = eligible
                .Select(w =>
                {
                    int fairness = (maxShifts - workerShiftCount.GetValueOrDefault(w.Id, 0)) * 10;
                    int clustering = workerDayAssigned.Contains((w.Id, slot.Date)) ? 5 : 0;
                    int barnConsistency = workerBarnHistory.Contains((w.Id, slot.Barn)) ? 2 : 0;
                    return (Worker: w, Score: fairness + clustering + barnConsistency);
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(_ => _random.Next()) // tie-break randomly
                .First();

            var winner = scored.Worker;
            assignments.Add(new ShiftAssignment
            {
                Date = slot.Date,
                Barn = slot.Barn,
                Shift = slot.Shift,
                WorkerId = winner.Id,
                WorkerName = winner.DisplayName
            });

            workerShiftCount[winner.Id] = workerShiftCount.GetValueOrDefault(winner.Id, 0) + 1;
            workerTimeslotBarn[(winner.Id, slot.Date, slot.Shift)] = slot.Barn;
            workerDayAssigned.Add((winner.Id, slot.Date));
            workerBarnHistory.Add((winner.Id, slot.Barn));
        }

        int filled = assignments.Count(a => a.WorkerId != "");
        int unfilled = assignments.Count(a => a.WorkerId == "");
        _logger.LogInformation("Schedule generated: {Filled} filled, {Unfilled} unfilled out of {Total} slots",
            filled, unfilled, assignments.Count);

        return new Schedule
        {
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            GeneratedAt = DateTime.UtcNow,
            Assignments = assignments
        };
    }

    private static bool IsEligible(
        Worker worker,
        DateOnly date,
        ShiftTime shift,
        Barn barn,
        Dictionary<(string, DateOnly), Availability> availabilityLookup,
        Dictionary<(string, DateOnly, ShiftTime), Barn> workerTimeslotBarn)
    {
        // Check availability — default to Available if no record
        if (availabilityLookup.TryGetValue((worker.Id, date), out var avail))
        {
            switch (avail.Status)
            {
                case AvailabilityStatus.NotAvailable:
                    return false;
                case AvailabilityStatus.MorningOnly when shift != ShiftTime.Morning:
                    return false;
                case AvailabilityStatus.EveningOnly when shift != ShiftTime.Evening:
                    return false;
            }
        }

        // Cannot be assigned to both barns for the same timeslot
        if (workerTimeslotBarn.TryGetValue((worker.Id, date, shift), out var assignedBarn) && assignedBarn != barn)
        {
            return false;
        }

        // Also cannot be assigned to the same barn+timeslot twice (already taken)
        if (workerTimeslotBarn.ContainsKey((worker.Id, date, shift)))
        {
            return false;
        }

        return true;
    }

    private static bool IsBlackedOut(
        DateOnly date,
        Barn barn,
        ShiftTime shift,
        HashSet<(DateOnly Date, Barn? Barn, ShiftTime? Shift)> blackoutSet)
    {
        // Whole-day blackout (all barns, all shifts)
        if (blackoutSet.Contains((date, null, null)))
            return true;
        // Barn-specific blackout (all shifts at this barn)
        if (blackoutSet.Contains((date, barn, null)))
            return true;
        // Shift-specific blackout (all barns for this shift)
        if (blackoutSet.Contains((date, null, shift)))
            return true;
        // Exact barn+shift blackout
        if (blackoutSet.Contains((date, barn, shift)))
            return true;
        return false;
    }
}
