using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Functions;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Tests.Functions;

public class AvailabilityFunctionsTests
{
    private readonly Mock<IAvailabilityService> _mockService;
    private readonly Mock<IWorkerRepository> _mockWorkerRepo;
    private readonly AvailabilityFunctions _functions;

    public AvailabilityFunctionsTests()
    {
        _mockService = new Mock<IAvailabilityService>();
        _mockWorkerRepo = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<AvailabilityFunctions>>();
        _functions = new AvailabilityFunctions(_mockService.Object, _mockWorkerRepo.Object, logger.Object);
    }

    private static HttpRequest CreateRequest(string? userId = null, string? userDetails = null, string? body = null, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        if (userId != null)
        {
            var json = JsonSerializer.Serialize(new { userId, userDetails });
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            context.Request.Headers["x-ms-client-principal"] = base64;
        }

        if (body != null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.ContentType = "application/json";
        }

        return context.Request;
    }

    [Fact]
    public async Task GetAvailability_ReturnsUserAvailability()
    {
        var expected = new List<Availability>
        {
            new() { WorkerId = "user-1", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };
        _mockService.Setup(x => x.GetAvailabilityAsync("2024-01-15", "user-1")).ReturnsAsync(expected);

        var req = CreateRequest(userId: "user-1", userDetails: "Test User");
        var result = await _functions.GetAvailability(req, "2024-01-15");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAvailability_Returns401_WhenNoAuthHeader()
    {
        var req = CreateRequest();
        var result = await _functions.GetAvailability(req, "2024-01-15");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PutAvailability_SavesAvailability()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("user-1"))
            .ReturnsAsync(new Worker { Id = "user-1", IsAdmin = false, IsActive = true, DisplayName = "Test User" });

        var items = new List<Availability>
        {
            new() { Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available },
            new() { Date = new DateOnly(2024, 1, 16), Status = AvailabilityStatus.MorningOnly }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var body = JsonSerializer.Serialize(items, jsonOptions);

        var req = CreateRequest(userId: "user-1", userDetails: "Test User", body: body, method: "PUT");
        var result = await _functions.PutAvailability(req, "2024-01-15");

        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(x => x.SetAvailabilityAsync("2024-01-15", "user-1", It.IsAny<IReadOnlyList<Availability>>()), Times.Once);
    }

    [Fact]
    public async Task PutAvailability_Returns401_WhenNoAuthHeader()
    {
        var req = CreateRequest(body: "[]", method: "PUT");
        var result = await _functions.PutAvailability(req, "2024-01-15");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PutAvailability_Returns403_WhenWorkerIsInactive()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("user-1"))
            .ReturnsAsync(new Worker { Id = "user-1", IsAdmin = false, IsActive = false, DisplayName = "Inactive" });

        var items = new List<Availability>
        {
            new() { Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var body = JsonSerializer.Serialize(items, jsonOptions);

        var req = CreateRequest(userId: "user-1", userDetails: "Inactive User", body: body, method: "PUT");
        var result = await _functions.PutAvailability(req, "2024-01-15");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AdminGetAvailability_Returns401_WhenNoAuth()
    {
        var req = CreateRequest();
        var result = await _functions.AdminGetAvailability(req, "2024-01-15", "worker-1");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdminGetAvailability_Returns403_WhenNotAdmin()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("user-1"))
            .ReturnsAsync(new Worker { Id = "user-1", IsAdmin = false, IsActive = true, DisplayName = "User" });

        var req = CreateRequest(userId: "user-1", userDetails: "User");
        var result = await _functions.AdminGetAvailability(req, "2024-01-15", "worker-1");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AdminGetAvailability_ReturnsWorkerAvailability()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("admin-1"))
            .ReturnsAsync(new Worker { Id = "admin-1", IsAdmin = true, IsActive = true, DisplayName = "Admin" });
        var expected = new List<Availability>
        {
            new() { WorkerId = "worker-1", Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };
        _mockService.Setup(x => x.GetAvailabilityAsync("2024-01-15", "worker-1")).ReturnsAsync(expected);

        var req = CreateRequest(userId: "admin-1", userDetails: "Admin");
        var result = await _functions.AdminGetAvailability(req, "2024-01-15", "worker-1");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task AdminPutAvailability_Returns401_WhenNoAuth()
    {
        var req = CreateRequest(body: "[]", method: "PUT");
        var result = await _functions.AdminPutAvailability(req, "2024-01-15", "worker-1");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdminPutAvailability_Returns403_WhenNotAdmin()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("user-1"))
            .ReturnsAsync(new Worker { Id = "user-1", IsAdmin = false, IsActive = true, DisplayName = "User" });

        var req = CreateRequest(userId: "user-1", userDetails: "User", body: "[]", method: "PUT");
        var result = await _functions.AdminPutAvailability(req, "2024-01-15", "worker-1");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AdminPutAvailability_SavesWorkerAvailability()
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync("admin-1"))
            .ReturnsAsync(new Worker { Id = "admin-1", IsAdmin = true, IsActive = true, DisplayName = "Admin" });

        var items = new List<Availability>
        {
            new() { Date = new DateOnly(2024, 1, 15), Status = AvailabilityStatus.Available }
        };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var body = JsonSerializer.Serialize(items, jsonOptions);

        var req = CreateRequest(userId: "admin-1", userDetails: "Admin", body: body, method: "PUT");
        var result = await _functions.AdminPutAvailability(req, "2024-01-15", "worker-1");

        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(x => x.SetAvailabilityAsync("2024-01-15", "worker-1", It.IsAny<IReadOnlyList<Availability>>()), Times.Once);
    }
}
