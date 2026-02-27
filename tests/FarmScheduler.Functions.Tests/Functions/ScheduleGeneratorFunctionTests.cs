using System.Text;
using System.Text.Json;
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
    private readonly Mock<IBarnConfigRepository> _mockBarnConfigRepo;
    private readonly Mock<IBlackoutRepository> _mockBlackoutRepo;
    private readonly Mock<ServiceBusClient> _mockServiceBusClient;
    private readonly Mock<ServiceBusSender> _mockSender;
    private readonly ScheduleGeneratorFunction _function;

    public ScheduleGeneratorFunctionTests()
    {
        _mockWorkerRepo = new Mock<IWorkerRepository>();
        _mockAvailabilityService = new Mock<IAvailabilityService>();
        _mockSchedulingService = new Mock<ISchedulingService>();
        _mockBarnConfigRepo = new Mock<IBarnConfigRepository>();
        _mockBlackoutRepo = new Mock<IBlackoutRepository>();
        _mockServiceBusClient = new Mock<ServiceBusClient>();
        _mockSender = new Mock<ServiceBusSender>();
        var logger = new Mock<ILogger<ScheduleGeneratorFunction>>();

        _mockServiceBusClient
            .Setup(x => x.CreateSender("schedule-generated"))
            .Returns(_mockSender.Object);

        _mockBarnConfigRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<BarnConfig>());
        _mockBlackoutRepo.Setup(x => x.GetForWindowAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BlackoutDate>());

        _function = new ScheduleGeneratorFunction(
            _mockWorkerRepo.Object,
            _mockAvailabilityService.Object,
            _mockSchedulingService.Object,
            _mockBarnConfigRepo.Object,
            _mockBlackoutRepo.Object,
            _mockServiceBusClient.Object,
            logger.Object);
    }

    private static HttpRequest CreateAdminRequest(string userId = "admin-1")
    {
        var context = new DefaultHttpContext();
        var json = JsonSerializer.Serialize(new { userId, userDetails = "Admin User" });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        context.Request.Headers["x-ms-client-principal"] = base64;
        return context.Request;
    }

    private void SetupScheduleGeneration()
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
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<IReadOnlyList<BarnConfig>>(), It.IsAny<IReadOnlyList<BlackoutDate>>()))
            .Returns(schedule);
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
            .Setup(x => x.GenerateSchedule(workers, availability, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<IReadOnlyList<BarnConfig>>(), It.IsAny<IReadOnlyList<BlackoutDate>>()))
            .Returns(schedule);

        var result = await _function.GenerateAndPublishScheduleAsync();

        result.Should().NotBeNull();
        _mockWorkerRepo.Verify(x => x.GetAllActiveAsync(), Times.Once);
        _mockAvailabilityService.Verify(x => x.GetAvailabilityAsync(It.IsAny<string>(), null), Times.Once);
        _mockSchedulingService.Verify(x => x.GenerateSchedule(
            workers, availability, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<IReadOnlyList<BarnConfig>>(), It.IsAny<IReadOnlyList<BlackoutDate>>()), Times.Once);
        _mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default), Times.Once);
    }

    [Fact]
    public async Task RunHttp_ReturnsOk_WhenAdmin()
    {
        var adminWorker = new Worker { Id = "admin-1", DisplayName = "Admin", IsActive = true, IsAdmin = true };
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("admin-1")).ReturnsAsync(adminWorker);
        SetupScheduleGeneration();

        var req = CreateAdminRequest();
        var result = await _function.RunHttp(req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunHttp_Returns401_WhenNoAuth()
    {
        var context = new DefaultHttpContext();
        var result = await _function.RunHttp(context.Request);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RunHttp_Returns403_WhenNotAdmin()
    {
        var worker = new Worker { Id = "user-1", DisplayName = "User", IsActive = true, IsAdmin = false };
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("user-1")).ReturnsAsync(worker);

        var req = CreateAdminRequest(userId: "user-1");
        var result = await _function.RunHttp(req);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RunHttp_Returns403_WhenUserNotFound()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("unknown")).ReturnsAsync((Worker?)null);

        var req = CreateAdminRequest(userId: "unknown");
        var result = await _function.RunHttp(req);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GenerateAndPublishScheduleAsync_PublishesToServiceBus()
    {
        SetupScheduleGeneration();

        await _function.GenerateAndPublishScheduleAsync();

        _mockServiceBusClient.Verify(x => x.CreateSender("schedule-generated"), Times.Once);
        _mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default), Times.Once);
    }
}
