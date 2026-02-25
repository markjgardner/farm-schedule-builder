using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using FluentAssertions;
using Moq;

namespace FarmScheduler.Integration.Tests;

public class AvailabilityServiceIntegrationTests
{
    private readonly Mock<IAvailabilityRepository> _mockRepo;
    private readonly AvailabilityService _service;
    private readonly Dictionary<string, List<Availability>> _store;

    private static readonly DateOnly WindowStart = new(2025, 1, 6);
    private static readonly string WindowStartStr = "2025-01-06";

    public AvailabilityServiceIntegrationTests()
    {
        _mockRepo = new Mock<IAvailabilityRepository>();
        _store = new Dictionary<string, List<Availability>>();
        SetupMockRepository();
        _service = new AvailabilityService(_mockRepo.Object);
    }

    /// <summary>
    /// Configures the mock repository to behave like an in-memory store for round-trip testing.
    /// </summary>
    private void SetupMockRepository()
    {
        _mockRepo.Setup(r => r.UpsertBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Availability>>()))
            .Returns<string, IReadOnlyList<Availability>>((window, items) =>
            {
                if (!_store.ContainsKey(window))
                    _store[window] = new List<Availability>();

                foreach (var item in items)
                {
                    _store[window].RemoveAll(a => a.WorkerId == item.WorkerId && a.Date == item.Date);
                    _store[window].Add(new Availability
                    {
                        WorkerId = item.WorkerId,
                        Date = item.Date,
                        Status = item.Status
                    });
                }
                return Task.CompletedTask;
            });

        _mockRepo.Setup(r => r.GetByWindowAsync(It.IsAny<string>()))
            .Returns<string>(window =>
            {
                var result = _store.ContainsKey(window) ? _store[window].ToList() : new List<Availability>();
                return Task.FromResult<IReadOnlyList<Availability>>(result);
            });

        _mockRepo.Setup(r => r.GetByWindowAndWorkerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((window, workerId) =>
            {
                var result = _store.ContainsKey(window)
                    ? _store[window].Where(a => a.WorkerId == workerId).ToList()
                    : new List<Availability>();
                return Task.FromResult<IReadOnlyList<Availability>>(result);
            });
    }

    private static List<Availability> CreateAvailabilityForWorker(string workerId, DateOnly start, int days, AvailabilityStatus status)
    {
        return Enumerable.Range(0, days)
            .Select(i => new Availability
            {
                WorkerId = workerId,
                Date = start.AddDays(i),
                Status = status
            })
            .ToList();
    }

    [Fact]
    public async Task SetAndRetrieve_MultipleWorkers_TwoWeekWindow()
    {
        // Set availability for 4 workers across 14 days
        var w1Avail = CreateAvailabilityForWorker("w1", WindowStart, 14, AvailabilityStatus.Available);
        var w2Avail = CreateAvailabilityForWorker("w2", WindowStart, 14, AvailabilityStatus.MorningOnly);
        var w3Avail = CreateAvailabilityForWorker("w3", WindowStart, 14, AvailabilityStatus.EveningOnly);
        var w4Avail = CreateAvailabilityForWorker("w4", WindowStart, 14, AvailabilityStatus.NotAvailable);

        await _service.SetAvailabilityAsync(WindowStartStr, "w1", w1Avail);
        await _service.SetAvailabilityAsync(WindowStartStr, "w2", w2Avail);
        await _service.SetAvailabilityAsync(WindowStartStr, "w3", w3Avail);
        await _service.SetAvailabilityAsync(WindowStartStr, "w4", w4Avail);

        // Retrieve all availability for the window
        var allAvailability = await _service.GetAvailabilityAsync(WindowStartStr);

        allAvailability.Should().HaveCount(56, "4 workers × 14 days = 56 records");
        allAvailability.Where(a => a.WorkerId == "w1").Should().HaveCount(14);
        allAvailability.Where(a => a.WorkerId == "w2").Should().HaveCount(14);
        allAvailability.Where(a => a.WorkerId == "w3").Should().HaveCount(14);
        allAvailability.Where(a => a.WorkerId == "w4").Should().HaveCount(14);
    }

    [Fact]
    public async Task RetrieveByWorker_ReturnsOnlyThatWorkersRecords()
    {
        var w1Avail = CreateAvailabilityForWorker("w1", WindowStart, 14, AvailabilityStatus.Available);
        var w2Avail = CreateAvailabilityForWorker("w2", WindowStart, 14, AvailabilityStatus.MorningOnly);

        await _service.SetAvailabilityAsync(WindowStartStr, "w1", w1Avail);
        await _service.SetAvailabilityAsync(WindowStartStr, "w2", w2Avail);

        var w1Result = await _service.GetAvailabilityAsync(WindowStartStr, "w1");

        w1Result.Should().HaveCount(14);
        w1Result.Should().OnlyContain(a => a.WorkerId == "w1");
        w1Result.Should().OnlyContain(a => a.Status == AvailabilityStatus.Available);
    }

    [Fact]
    public async Task RoundTrip_DataIntegrity_PreservesAllFields()
    {
        var availability = new List<Availability>
        {
            new() { Date = WindowStart, Status = AvailabilityStatus.Available },
            new() { Date = WindowStart.AddDays(1), Status = AvailabilityStatus.MorningOnly },
            new() { Date = WindowStart.AddDays(2), Status = AvailabilityStatus.EveningOnly },
            new() { Date = WindowStart.AddDays(3), Status = AvailabilityStatus.NotAvailable },
            new() { Date = WindowStart.AddDays(4), Status = AvailabilityStatus.Available },
        };

        await _service.SetAvailabilityAsync(WindowStartStr, "w1", availability);
        var retrieved = await _service.GetAvailabilityAsync(WindowStartStr, "w1");

        retrieved.Should().HaveCount(5);

        retrieved.Should().ContainSingle(a => a.Date == WindowStart && a.Status == AvailabilityStatus.Available);
        retrieved.Should().ContainSingle(a => a.Date == WindowStart.AddDays(1) && a.Status == AvailabilityStatus.MorningOnly);
        retrieved.Should().ContainSingle(a => a.Date == WindowStart.AddDays(2) && a.Status == AvailabilityStatus.EveningOnly);
        retrieved.Should().ContainSingle(a => a.Date == WindowStart.AddDays(3) && a.Status == AvailabilityStatus.NotAvailable);
        retrieved.Should().ContainSingle(a => a.Date == WindowStart.AddDays(4) && a.Status == AvailabilityStatus.Available);

        // All records should have the correct worker ID set by the service
        retrieved.Should().OnlyContain(a => a.WorkerId == "w1");
    }

    [Fact]
    public async Task SetAvailability_OverwritesExistingRecords()
    {
        var initial = CreateAvailabilityForWorker("w1", WindowStart, 7, AvailabilityStatus.Available);
        await _service.SetAvailabilityAsync(WindowStartStr, "w1", initial);

        // Overwrite with different status
        var updated = CreateAvailabilityForWorker("w1", WindowStart, 7, AvailabilityStatus.NotAvailable);
        await _service.SetAvailabilityAsync(WindowStartStr, "w1", updated);

        var result = await _service.GetAvailabilityAsync(WindowStartStr, "w1");

        result.Should().HaveCount(7);
        result.Should().OnlyContain(a => a.Status == AvailabilityStatus.NotAvailable);
    }

    [Fact]
    public async Task EmptyWindow_ReturnsEmptyList()
    {
        var result = await _service.GetAvailabilityAsync("2099-01-01");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NonExistentWorker_ReturnsEmptyList()
    {
        var w1Avail = CreateAvailabilityForWorker("w1", WindowStart, 14, AvailabilityStatus.Available);
        await _service.SetAvailabilityAsync(WindowStartStr, "w1", w1Avail);

        var result = await _service.GetAvailabilityAsync(WindowStartStr, "non-existent");

        result.Should().BeEmpty();
    }
}
