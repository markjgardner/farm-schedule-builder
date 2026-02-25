using System.Text.Json;
using System.Text.Json.Serialization;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FarmScheduler.Integration.Tests;

public class EndToEndWorkflowTests
{
    private const int FixedSeed = 42;
    private static readonly DateOnly WindowStart = new(2025, 1, 6);
    private static readonly DateOnly WindowEnd = new(2025, 1, 19);
    private static readonly string WindowStartStr = "2025-01-06";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Mock<IWorkerRepository> _mockWorkerRepo;
    private readonly Mock<IAvailabilityRepository> _mockAvailRepo;
    private readonly AvailabilityService _availabilityService;
    private readonly SchedulingService _schedulingService;

    // In-memory stores
    private readonly List<Worker> _workerStore = new();
    private readonly Dictionary<string, List<Availability>> _availStore = new();

    public EndToEndWorkflowTests()
    {
        _mockWorkerRepo = new Mock<IWorkerRepository>();
        _mockAvailRepo = new Mock<IAvailabilityRepository>();

        SetupWorkerRepository();
        SetupAvailabilityRepository();

        _availabilityService = new AvailabilityService(_mockAvailRepo.Object);
        _schedulingService = new SchedulingService(Mock.Of<ILogger<SchedulingService>>(), FixedSeed);
    }

    private void SetupWorkerRepository()
    {
        _mockWorkerRepo.Setup(r => r.UpsertAsync(It.IsAny<Worker>()))
            .Returns<Worker>(worker =>
            {
                _workerStore.RemoveAll(w => w.Id == worker.Id);
                _workerStore.Add(worker);
                return Task.CompletedTask;
            });

        _mockWorkerRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(() => _workerStore.Where(w => w.IsActive).ToList());

        _mockWorkerRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .Returns<string>(id => Task.FromResult(_workerStore.FirstOrDefault(w => w.Id == id)));
    }

    private void SetupAvailabilityRepository()
    {
        _mockAvailRepo.Setup(r => r.UpsertBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Availability>>()))
            .Returns<string, IReadOnlyList<Availability>>((window, items) =>
            {
                if (!_availStore.ContainsKey(window))
                    _availStore[window] = new List<Availability>();

                foreach (var item in items)
                {
                    _availStore[window].RemoveAll(a => a.WorkerId == item.WorkerId && a.Date == item.Date);
                    _availStore[window].Add(new Availability
                    {
                        WorkerId = item.WorkerId,
                        Date = item.Date,
                        Status = item.Status
                    });
                }
                return Task.CompletedTask;
            });

        _mockAvailRepo.Setup(r => r.GetByWindowAsync(It.IsAny<string>()))
            .Returns<string>(window =>
            {
                var result = _availStore.ContainsKey(window) ? _availStore[window].ToList() : new List<Availability>();
                return Task.FromResult<IReadOnlyList<Availability>>(result);
            });

        _mockAvailRepo.Setup(r => r.GetByWindowAndWorkerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((window, workerId) =>
            {
                var result = _availStore.ContainsKey(window)
                    ? _availStore[window].Where(a => a.WorkerId == workerId).ToList()
                    : new List<Availability>();
                return Task.FromResult<IReadOnlyList<Availability>>(result);
            });
    }

    [Fact]
    public async Task FullWorkflow_RegisterWorkers_SetAvailability_GenerateSchedule()
    {
        // Step 1: Register workers
        var workers = new List<Worker>
        {
            new() { Id = "w1", DisplayName = "Alice", Email = "alice@farm.com", IsActive = true },
            new() { Id = "w2", DisplayName = "Bob", Email = "bob@farm.com", IsActive = true },
            new() { Id = "w3", DisplayName = "Charlie", Email = "charlie@farm.com", IsActive = true },
            new() { Id = "w4", DisplayName = "Diana", Email = "diana@farm.com", IsActive = true },
        };

        foreach (var worker in workers)
            await _mockWorkerRepo.Object.UpsertAsync(worker);

        var activeWorkers = await _mockWorkerRepo.Object.GetAllActiveAsync();
        activeWorkers.Should().HaveCount(4);

        // Step 2: Submit availability
        foreach (var worker in workers)
        {
            var availability = Enumerable.Range(0, 14)
                .Select(i => new Availability
                {
                    Date = WindowStart.AddDays(i),
                    Status = AvailabilityStatus.Available
                })
                .ToList();

            await _availabilityService.SetAvailabilityAsync(WindowStartStr, worker.Id, availability);
        }

        var allAvailability = await _availabilityService.GetAvailabilityAsync(WindowStartStr);
        allAvailability.Should().HaveCount(56);

        // Step 3: Generate schedule
        var schedule = _schedulingService.GenerateSchedule(activeWorkers, allAvailability, WindowStart, WindowEnd);

        schedule.Assignments.Should().HaveCount(56);
        schedule.Assignments.Should().OnlyContain(a => a.WorkerId != "");

        // Step 4: Serialize (mimicking Service Bus publish)
        var json = JsonSerializer.Serialize(schedule, JsonOptions);
        json.Should().NotBeNullOrEmpty();

        // Step 5: Deserialize and verify round-trip
        var deserialized = JsonSerializer.Deserialize<Schedule>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.WindowStart.Should().Be(WindowStart);
        deserialized.WindowEnd.Should().Be(WindowEnd);
        deserialized.Assignments.Should().HaveCount(56);
    }

    [Fact]
    public async Task FullWorkflow_WithConstraints_GeneratesValidSchedule()
    {
        // Register workers
        var workers = new List<Worker>
        {
            new() { Id = "w1", DisplayName = "Alice", Email = "alice@farm.com", IsActive = true },
            new() { Id = "w2", DisplayName = "Bob", Email = "bob@farm.com", IsActive = true },
            new() { Id = "w3", DisplayName = "Charlie", Email = "charlie@farm.com", IsActive = true },
            new() { Id = "w4", DisplayName = "Diana", Email = "diana@farm.com", IsActive = true },
            new() { Id = "w5", DisplayName = "Eve", Email = "eve@farm.com", IsActive = true },
        };

        foreach (var worker in workers)
            await _mockWorkerRepo.Object.UpsertAsync(worker);

        // Set mixed availability
        for (int i = 0; i < 14; i++)
        {
            var date = WindowStart.AddDays(i);

            // Alice: available all days
            await _availabilityService.SetAvailabilityAsync(WindowStartStr, "w1",
                new List<Availability> { new() { Date = date, Status = AvailabilityStatus.Available } });

            // Bob: morning only on weekdays, not available weekends
            var bobStatus = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? AvailabilityStatus.NotAvailable
                : AvailabilityStatus.MorningOnly;
            await _availabilityService.SetAvailabilityAsync(WindowStartStr, "w2",
                new List<Availability> { new() { Date = date, Status = bobStatus } });

            // Charlie: evening only all days
            await _availabilityService.SetAvailabilityAsync(WindowStartStr, "w3",
                new List<Availability> { new() { Date = date, Status = AvailabilityStatus.EveningOnly } });

            // Diana: available except Fridays
            var dianaStatus = date.DayOfWeek == DayOfWeek.Friday
                ? AvailabilityStatus.NotAvailable
                : AvailabilityStatus.Available;
            await _availabilityService.SetAvailabilityAsync(WindowStartStr, "w4",
                new List<Availability> { new() { Date = date, Status = dianaStatus } });

            // Eve: available all days
            await _availabilityService.SetAvailabilityAsync(WindowStartStr, "w5",
                new List<Availability> { new() { Date = date, Status = AvailabilityStatus.Available } });
        }

        var activeWorkers = await _mockWorkerRepo.Object.GetAllActiveAsync();
        var allAvailability = await _availabilityService.GetAvailabilityAsync(WindowStartStr);

        var schedule = _schedulingService.GenerateSchedule(activeWorkers, allAvailability, WindowStart, WindowEnd);

        // Verify constraints
        schedule.Assignments.Should().HaveCount(56);

        // Bob should never have evening shifts
        schedule.Assignments
            .Where(a => a.WorkerId == "w2" && a.Shift == ShiftTime.Evening)
            .Should().BeEmpty("Bob is MorningOnly on weekdays, NotAvailable weekends");

        // Charlie should never have morning shifts
        schedule.Assignments
            .Where(a => a.WorkerId == "w3" && a.Shift == ShiftTime.Morning)
            .Should().BeEmpty("Charlie is EveningOnly");

        // Diana should not be scheduled on Fridays
        var fridays = Enumerable.Range(0, 14)
            .Select(i => WindowStart.AddDays(i))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday)
            .ToList();

        schedule.Assignments
            .Where(a => a.WorkerId == "w4" && fridays.Contains(a.Date))
            .Should().BeEmpty("Diana is NotAvailable on Fridays");

        // No conflicts
        var conflicts = schedule.Assignments
            .Where(a => a.WorkerId != "")
            .GroupBy(a => (a.Date, a.Shift, a.WorkerId))
            .Where(g => g.Count() > 1)
            .ToList();
        conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task ScheduleJson_SerializesCorrectly_ForServiceBusPublishing()
    {
        // Register workers and generate a simple schedule
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w1", DisplayName = "Alice", Email = "alice@farm.com", IsActive = true });
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w2", DisplayName = "Bob", Email = "bob@farm.com", IsActive = true });

        var activeWorkers = await _mockWorkerRepo.Object.GetAllActiveAsync();
        var schedule = _schedulingService.GenerateSchedule(activeWorkers, new List<Availability>(), WindowStart, WindowEnd);

        var json = JsonSerializer.Serialize(schedule, JsonOptions);

        // Verify JSON structure
        json.Should().Contain("\"windowStart\":");
        json.Should().Contain("\"windowEnd\":");
        json.Should().Contain("\"generatedAt\":");
        json.Should().Contain("\"assignments\":");

        // Verify enums serialize as strings (for Service Bus consumers)
        (json.Contains("\"Windhover\"") || json.Contains("\"York\""))
            .Should().BeTrue("barn enums should serialize as strings");
        (json.Contains("\"Morning\"") || json.Contains("\"Evening\""))
            .Should().BeTrue("shift enums should serialize as strings");

        // Verify round-trip
        var deserialized = JsonSerializer.Deserialize<Schedule>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.Assignments.Should().HaveCount(schedule.Assignments.Count);
        deserialized.WindowStart.Should().Be(WindowStart);
        deserialized.WindowEnd.Should().Be(WindowEnd);

        // Verify individual assignment round-trip
        var firstOriginal = schedule.Assignments.First();
        var firstDeserialized = deserialized.Assignments.First();
        firstDeserialized.Date.Should().Be(firstOriginal.Date);
        firstDeserialized.Barn.Should().Be(firstOriginal.Barn);
        firstDeserialized.Shift.Should().Be(firstOriginal.Shift);
        firstDeserialized.WorkerId.Should().Be(firstOriginal.WorkerId);
        firstDeserialized.WorkerName.Should().Be(firstOriginal.WorkerName);
    }

    [Fact]
    public async Task FullWorkflow_InactiveWorkersExcluded()
    {
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w1", DisplayName = "Active Worker", Email = "active@farm.com", IsActive = true });
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w2", DisplayName = "Inactive Worker", Email = "inactive@farm.com", IsActive = false });

        var activeWorkers = await _mockWorkerRepo.Object.GetAllActiveAsync();
        activeWorkers.Should().HaveCount(1);
        activeWorkers.Should().OnlyContain(w => w.Id == "w1");

        var schedule = _schedulingService.GenerateSchedule(
            activeWorkers, new List<Availability>(), WindowStart, WindowEnd);

        schedule.Assignments.Where(a => a.WorkerId == "w2").Should().BeEmpty(
            "inactive workers should not appear in the schedule");
    }

    [Fact]
    public async Task WorkerLookup_ById_ReturnsCorrectWorker()
    {
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w1", DisplayName = "Alice", Email = "alice@farm.com", IsActive = true });
        await _mockWorkerRepo.Object.UpsertAsync(
            new Worker { Id = "w2", DisplayName = "Bob", Email = "bob@farm.com", IsActive = true });

        var worker = await _mockWorkerRepo.Object.GetByIdAsync("w1");
        worker.Should().NotBeNull();
        worker!.DisplayName.Should().Be("Alice");

        var nonExistent = await _mockWorkerRepo.Object.GetByIdAsync("non-existent");
        nonExistent.Should().BeNull();
    }
}
