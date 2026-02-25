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
    public async Task RegisterWorker_CreatesWorkerFromAuthHeader()
    {
        var req = CreateRequest(userId: "user-123", userDetails: "Jane Doe");
        var result = await _functions.RegisterWorker(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.Id.Should().Be("user-123");
        worker.DisplayName.Should().Be("Jane Doe");
        worker.IsActive.Should().BeTrue();

        _mockRepo.Verify(x => x.UpsertAsync(It.Is<Worker>(w =>
            w.Id == "user-123" && w.DisplayName == "Jane Doe" && w.IsActive)), Times.Once);
    }

    [Fact]
    public async Task RegisterWorker_Returns401_WhenNoAuthHeader()
    {
        var req = CreateRequest();
        var result = await _functions.RegisterWorker(req);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RegisterWorker_UsesUserIdAsDisplayName_WhenUserDetailsNull()
    {
        var json = JsonSerializer.Serialize(new { userId = "user-456" });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = await _functions.RegisterWorker(context.Request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.DisplayName.Should().Be("user-456");
    }
}
