using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Functions;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Tests.Functions;

public class ScheduleGeneratorFunctionTests
{
    private readonly Mock<IWorkerRepository> _mockWorkerRepo;
    private readonly Mock<IAvailabilityService> _mockAvailabilityService;
    private readonly Mock<ISchedulingService> _mockSchedulingService;
    private readonly Mock<ServiceBusClient> _mockServiceBusClient;
    private readonly Mock<ServiceBusSender> _mockSender;
    private readonly ScheduleGeneratorFunction _function;

    public ScheduleGeneratorFunctionTests()
    {
        _mockWorkerRepo = new Mock<IWorkerRepository>();
        _mockAvailabilityService = new Mock<IAvailabilityService>();
        _mockSchedulingService = new Mock<ISchedulingService>();
        _mockServiceBusClient = new Mock<ServiceBusClient>();
        _mockSender = new Mock<ServiceBusSender>();
        var logger = new Mock<ILogger<ScheduleGeneratorFunction>>();

        _mockServiceBusClient
            .Setup(x => x.CreateSender("schedule-generated"))
            .Returns(_mockSender.Object);

        _function = new ScheduleGeneratorFunction(
            _mockWorkerRepo.Object,
            _mockAvailabilityService.Object,
            _mockSchedulingService.Object,
            _mockServiceBusClient.Object,
            logger.Object);
    }

    [Fact]
    public async Task GenerateAndPublishScheduleAsync_GeneratesAndPublishes()
    {
        var workers = new List<Worker>
        {
            new() { Id = "w1", DisplayName = "Alice", IsActive = true }
        };

        var availability = new List<Availability>
        {
            new() { WorkerId = "w1", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };

        var schedule = new Schedule
        {
            WindowStart = new DateOnly(2024, 1, 15),
            WindowEnd = new DateOnly(2024, 1, 28),
            Assignments = new List<ShiftAssignment>
            {
                new() { Date = new DateOnly(2024, 1, 15), Barn = Barn.Windhover, Shift = ShiftTime.Morning, WorkerId = "w1", WorkerName = "Alice" }
            }
        };

        _mockWorkerRepo.Setup(x => x.GetAllActiveAsync()).ReturnsAsync(workers);
        _mockAvailabilityService.Setup(x => x.GetAvailabilityAsync(It.IsAny<string>(), null)).ReturnsAsync(availability);
        _mockSchedulingService
            .Setup(x => x.GenerateSchedule(workers, availability, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(schedule);

        var result = await _function.GenerateAndPublishScheduleAsync();

        result.Should().NotBeNull();
        _mockWorkerRepo.Verify(x => x.GetAllActiveAsync(), Times.Once);
        _mockAvailabilityService.Verify(x => x.GetAvailabilityAsync(It.IsAny<string>(), null), Times.Once);
        _mockSchedulingService.Verify(x => x.GenerateSchedule(
            workers, availability, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Once);
        _mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default), Times.Once);
    }

    [Fact]
    public async Task RunHttp_ReturnsOkWithSchedule()
    {
        var workers = new List<Worker>();
        var availability = new List<Availability>();
        var schedule = new Schedule
        {
            WindowStart = new DateOnly(2024, 1, 15),
            WindowEnd = new DateOnly(2024, 1, 28),
            Assignments = new List<ShiftAssignment>()
        };

        _mockWorkerRepo.Setup(x => x.GetAllActiveAsync()).ReturnsAsync(workers);
        _mockAvailabilityService.Setup(x => x.GetAvailabilityAsync(It.IsAny<string>(), null)).ReturnsAsync(availability);
        _mockSchedulingService
            .Setup(x => x.GenerateSchedule(It.IsAny<IReadOnlyList<Worker>>(), It.IsAny<IReadOnlyList<Availability>>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(schedule);

        var context = new DefaultHttpContext();
        var result = await _function.RunHttp(context.Request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GenerateAndPublishScheduleAsync_PublishesToServiceBus()
    {
        var workers = new List<Worker>();
        var availability = new List<Availability>();
        var schedule = new Schedule
        {
            WindowStart = new DateOnly(2024, 1, 15),
            WindowEnd = new DateOnly(2024, 1, 28),
            Assignments = new List<ShiftAssignment>()
        };

        _mockWorkerRepo.Setup(x => x.GetAllActiveAsync()).ReturnsAsync(workers);
        _mockAvailabilityService.Setup(x => x.GetAvailabilityAsync(It.IsAny<string>(), null)).ReturnsAsync(availability);
        _mockSchedulingService
            .Setup(x => x.GenerateSchedule(It.IsAny<IReadOnlyList<Worker>>(), It.IsAny<IReadOnlyList<Availability>>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(schedule);

        await _function.GenerateAndPublishScheduleAsync();

        _mockServiceBusClient.Verify(x => x.CreateSender("schedule-generated"), Times.Once);
        _mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default), Times.Once);
    }
}
