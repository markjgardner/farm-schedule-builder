using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;

namespace FarmScheduler.Functions.Tests.Services;

public class AvailabilityServiceTests
{
    private readonly Mock<IAvailabilityRepository> _mockRepo;
    private readonly AvailabilityService _service;

    public AvailabilityServiceTests()
    {
        _mockRepo = new Mock<IAvailabilityRepository>();
        _service = new AvailabilityService(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAvailabilityAsync_WithWorkerId_CallsGetByWindowAndWorkerAsync()
    {
        var expected = new List<Availability>
        {
            new() { WorkerId = "w1", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };

        _mockRepo
            .Setup(x => x.GetByWindowAndWorkerAsync("2024-01-15", "w1"))
            .ReturnsAsync(expected);

        var result = await _service.GetAvailabilityAsync("2024-01-15", "w1");

        result.Should().BeEquivalentTo(expected);
        _mockRepo.Verify(x => x.GetByWindowAndWorkerAsync("2024-01-15", "w1"), Times.Once);
        _mockRepo.Verify(x => x.GetByWindowAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailabilityAsync_WithoutWorkerId_CallsGetByWindowAsync()
    {
        var expected = new List<Availability>
        {
            new() { WorkerId = "w1", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available },
            new() { WorkerId = "w2", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.MorningOnly }
        };

        _mockRepo
            .Setup(x => x.GetByWindowAsync("2024-01-15"))
            .ReturnsAsync(expected);

        var result = await _service.GetAvailabilityAsync("2024-01-15");

        result.Should().HaveCount(2);
        _mockRepo.Verify(x => x.GetByWindowAsync("2024-01-15"), Times.Once);
        _mockRepo.Verify(x => x.GetByWindowAndWorkerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailabilityAsync_WithNullWorkerId_CallsGetByWindowAsync()
    {
        _mockRepo
            .Setup(x => x.GetByWindowAsync("2024-01-15"))
            .ReturnsAsync(new List<Availability>());

        await _service.GetAvailabilityAsync("2024-01-15", null);

        _mockRepo.Verify(x => x.GetByWindowAsync("2024-01-15"), Times.Once);
        _mockRepo.Verify(x => x.GetByWindowAndWorkerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetAvailabilityAsync_SetsWorkerIdOnAllItems()
    {
        var items = new List<Availability>
        {
            new() { Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available },
            new() { Date = new DateOnly(2024, 1, 16), Status = AvailabilityStatus.MorningOnly },
            new() { Date = new DateOnly(2024, 1, 17), Status = AvailabilityStatus.EveningOnly }
        };

        await _service.SetAvailabilityAsync("2024-01-15", "w1", items);

        items.Should().AllSatisfy(i => i.WorkerId.Should().Be("w1"));
    }

    [Fact]
    public async Task SetAvailabilityAsync_CallsUpsertBatchAsync()
    {
        var items = new List<Availability>
        {
            new() { Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };

        await _service.SetAvailabilityAsync("2024-01-15", "w1", items);

        _mockRepo.Verify(x => x.UpsertBatchAsync("2024-01-15", items), Times.Once);
    }

    [Fact]
    public async Task SetAvailabilityAsync_OverwritesExistingWorkerId()
    {
        var items = new List<Availability>
        {
            new() { WorkerId = "old-worker", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };

        await _service.SetAvailabilityAsync("2024-01-15", "new-worker", items);

        items[0].WorkerId.Should().Be("new-worker");
    }
}
