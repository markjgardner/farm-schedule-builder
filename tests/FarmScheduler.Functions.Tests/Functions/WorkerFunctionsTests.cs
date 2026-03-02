using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Functions;
using FarmScheduler.Functions.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Tests.Functions;

public class WorkerFunctionsTests
{
    private readonly Mock<IWorkerRepository> _mockRepo;
    private readonly WorkerFunctions _functions;

    public WorkerFunctionsTests()
    {
        _mockRepo = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<WorkerFunctions>>();
        _functions = new WorkerFunctions(_mockRepo.Object, logger.Object);
    }

    private static HttpRequest CreateRequest(string? userId = null, string? userDetails = null)
    {
        var context = new DefaultHttpContext();

        if (userId != null)
        {
            var json = JsonSerializer.Serialize(new { userId, userDetails });
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            context.Request.Headers["x-ms-client-principal"] = base64;
        }

        return context.Request;
    }

    [Fact]
    public async Task GetWorkers_ReturnsAllActiveWorkers()
    {
        var expected = new List<Worker>
        {
            new() { Id = "w1", DisplayName = "Alice", IsActive = true },
            new() { Id = "w2", DisplayName = "Bob", IsActive = true }
        };
        _mockRepo.Setup(x => x.GetAllActiveAsync()).ReturnsAsync(expected);

        var req = CreateRequest();
        var result = await _functions.GetWorkers(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetMe_ReturnsWorker_WhenRegistered()
    {
        var worker = new Worker { Id = "user-123", DisplayName = "Jane Doe", IsActive = true, IsAdmin = false };
        _mockRepo.Setup(x => x.GetByIdAsync("user-123")).ReturnsAsync(worker);
        var req = CreateRequest(userId: "user-123", userDetails: "Jane Doe");
        var result = await _functions.GetMe(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<Worker>().Subject;
        returned.Id.Should().Be("user-123");
        returned.DisplayName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetMe_Returns401_WhenNoAuthHeader()
    {
        var req = CreateRequest();
        var result = await _functions.GetMe(req);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMe_Returns403_WhenUserNotInWorkersTable()
    {
        _mockRepo.Setup(x => x.GetByIdAsync("unknown-user")).ReturnsAsync((Worker?)null);
        var req = CreateRequest(userId: "unknown-user", userDetails: "Unknown");
        var result = await _functions.GetMe(req);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetMe_Returns403_WhenWorkerIsInactive()
    {
        var worker = new Worker { Id = "user-456", DisplayName = "Inactive", IsActive = false, IsAdmin = false };
        _mockRepo.Setup(x => x.GetByIdAsync("user-456")).ReturnsAsync(worker);
        var req = CreateRequest(userId: "user-456", userDetails: "Inactive User");
        var result = await _functions.GetMe(req);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetMe_ReturnsWorker_WhenInactiveAdmin()
    {
        var worker = new Worker { Id = "admin-2", DisplayName = "Inactive Admin", IsActive = false, IsAdmin = true };
        _mockRepo.Setup(x => x.GetByIdAsync("admin-2")).ReturnsAsync(worker);
        var req = CreateRequest(userId: "admin-2", userDetails: "Inactive Admin");
        var result = await _functions.GetMe(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<Worker>().Subject;
        returned.Id.Should().Be("admin-2");
        returned.IsAdmin.Should().BeTrue();
        returned.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetMe_ReturnsAdmin_WhenWorkerIsAdmin()
    {
        var worker = new Worker { Id = "admin-1", DisplayName = "Admin", IsActive = true, IsAdmin = true };
        _mockRepo.Setup(x => x.GetByIdAsync("admin-1")).ReturnsAsync(worker);
        var req = CreateRequest(userId: "admin-1", userDetails: "Admin User");
        var result = await _functions.GetMe(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<Worker>().Subject;
        returned.IsAdmin.Should().BeTrue();
    }
}
