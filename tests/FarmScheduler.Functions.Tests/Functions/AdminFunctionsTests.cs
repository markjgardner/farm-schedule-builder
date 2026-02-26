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

public class AdminFunctionsTests
{
    private readonly Mock<IWorkerRepository> _mockRepo;
    private readonly AdminFunctions _functions;

    public AdminFunctionsTests()
    {
        _mockRepo = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<AdminFunctions>>();
        _functions = new AdminFunctions(_mockRepo.Object, logger.Object);
    }

    private static HttpRequest CreateRequest(string? userId = null, bool isAdmin = false, string? jsonBody = null)
    {
        var context = new DefaultHttpContext();

        if (userId != null)
        {
            var json = JsonSerializer.Serialize(new { userId, userDetails = $"{userId}@example.com" });
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            context.Request.Headers["x-ms-client-principal"] = base64;
        }

        if (jsonBody != null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
            context.Request.ContentType = "application/json";
        }

        return context.Request;
    }

    private void SetupAdminWorker(string userId)
    {
        _mockRepo.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(new Worker { Id = userId, DisplayName = "Admin", IsActive = true, IsAdmin = true });
    }

    private void SetupNonAdminWorker(string userId)
    {
        _mockRepo.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(new Worker { Id = userId, DisplayName = "User", IsActive = true, IsAdmin = false });
    }

    // GET /api/admin/workers

    [Fact]
    public async Task GetWorkers_AdminCaller_ReturnsAllWorkers()
    {
        SetupAdminWorker("admin-1");
        var allWorkers = new List<Worker>
        {
            new() { Id = "w1", DisplayName = "Alice", IsActive = true },
            new() { Id = "w2", DisplayName = "Bob", IsActive = false }
        };
        _mockRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(allWorkers);

        var req = CreateRequest(userId: "admin-1", isAdmin: true);
        var result = await _functions.GetWorkers(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(allWorkers);
    }

    [Fact]
    public async Task GetWorkers_NonAdmin_Returns403()
    {
        SetupNonAdminWorker("user-1");

        var req = CreateRequest(userId: "user-1");
        var result = await _functions.GetWorkers(req);

        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetWorkers_Unauthenticated_Returns401()
    {
        var req = CreateRequest();
        var result = await _functions.GetWorkers(req);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // POST /api/admin/workers

    [Fact]
    public async Task CreateWorker_AdminCaller_CreatesWorker()
    {
        SetupAdminWorker("admin-1");

        var body = JsonSerializer.Serialize(new { displayName = "New Worker", email = "new@example.com" });
        var req = CreateRequest(userId: "admin-1", jsonBody: body);
        var result = await _functions.CreateWorker(req);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.DisplayName.Should().Be("New Worker");
        worker.Email.Should().Be("new@example.com");
        worker.Id.Should().StartWith("manual_");
        worker.IsActive.Should().BeTrue();

        _mockRepo.Verify(x => x.UpsertAsync(It.Is<Worker>(w =>
            w.DisplayName == "New Worker" && w.Id.StartsWith("manual_"))), Times.Once);
    }

    [Fact]
    public async Task CreateWorker_NonAdmin_Returns403()
    {
        SetupNonAdminWorker("user-1");

        var body = JsonSerializer.Serialize(new { displayName = "New Worker" });
        var req = CreateRequest(userId: "user-1", jsonBody: body);
        var result = await _functions.CreateWorker(req);

        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateWorker_Unauthenticated_Returns401()
    {
        var body = JsonSerializer.Serialize(new { displayName = "New Worker" });
        var req = CreateRequest(jsonBody: body);
        var result = await _functions.CreateWorker(req);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // PUT deactivate/activate

    [Fact]
    public async Task DeactivateWorker_AdminCaller_SetsInactive()
    {
        SetupAdminWorker("admin-1");
        _mockRepo.Setup(x => x.GetByIdAsync("w1"))
            .ReturnsAsync(new Worker { Id = "w1", DisplayName = "Alice", IsActive = true });

        var req = CreateRequest(userId: "admin-1");
        var result = await _functions.DeactivateWorker(req, "w1");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.IsActive.Should().BeFalse();

        _mockRepo.Verify(x => x.UpsertAsync(It.Is<Worker>(w => w.Id == "w1" && !w.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ActivateWorker_AdminCaller_SetsActive()
    {
        SetupAdminWorker("admin-1");
        _mockRepo.Setup(x => x.GetByIdAsync("w1"))
            .ReturnsAsync(new Worker { Id = "w1", DisplayName = "Alice", IsActive = false });

        var req = CreateRequest(userId: "admin-1");
        var result = await _functions.ActivateWorker(req, "w1");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.IsActive.Should().BeTrue();

        _mockRepo.Verify(x => x.UpsertAsync(It.Is<Worker>(w => w.Id == "w1" && w.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateWorker_NonAdmin_Returns403()
    {
        SetupNonAdminWorker("user-1");

        var req = CreateRequest(userId: "user-1");
        var result = await _functions.DeactivateWorker(req, "w1");

        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ActivateWorker_Unauthenticated_Returns401()
    {
        var req = CreateRequest();
        var result = await _functions.ActivateWorker(req, "w1");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // PUT admin toggle

    [Fact]
    public async Task ToggleAdmin_AdminCaller_SetsIsAdmin()
    {
        SetupAdminWorker("admin-1");
        _mockRepo.Setup(x => x.GetByIdAsync("w1"))
            .ReturnsAsync(new Worker { Id = "w1", DisplayName = "Alice", IsAdmin = false });

        var body = JsonSerializer.Serialize(new { isAdmin = true });
        var req = CreateRequest(userId: "admin-1", jsonBody: body);
        var result = await _functions.ToggleAdmin(req, "w1");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var worker = okResult.Value.Should().BeOfType<Worker>().Subject;
        worker.IsAdmin.Should().BeTrue();

        _mockRepo.Verify(x => x.UpsertAsync(It.Is<Worker>(w => w.Id == "w1" && w.IsAdmin)), Times.Once);
    }

    [Fact]
    public async Task ToggleAdmin_NonAdmin_Returns403()
    {
        SetupNonAdminWorker("user-1");

        var body = JsonSerializer.Serialize(new { isAdmin = true });
        var req = CreateRequest(userId: "user-1", jsonBody: body);
        var result = await _functions.ToggleAdmin(req, "w1");

        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ToggleAdmin_Unauthenticated_Returns401()
    {
        var body = JsonSerializer.Serialize(new { isAdmin = true });
        var req = CreateRequest(jsonBody: body);
        var result = await _functions.ToggleAdmin(req, "w1");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // DELETE

    [Fact]
    public async Task DeleteWorker_AdminCaller_RemovesWorker()
    {
        SetupAdminWorker("admin-1");
        _mockRepo.Setup(x => x.GetByIdAsync("w1"))
            .ReturnsAsync(new Worker { Id = "w1", DisplayName = "Alice" });

        var req = CreateRequest(userId: "admin-1");
        var result = await _functions.DeleteWorker(req, "w1");

        result.Should().BeOfType<OkResult>();
        _mockRepo.Verify(x => x.DeleteAsync("w1"), Times.Once);
    }

    [Fact]
    public async Task DeleteWorker_NonAdmin_Returns403()
    {
        SetupNonAdminWorker("user-1");

        var req = CreateRequest(userId: "user-1");
        var result = await _functions.DeleteWorker(req, "w1");

        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeleteWorker_Unauthenticated_Returns401()
    {
        var req = CreateRequest();
        var result = await _functions.DeleteWorker(req, "w1");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteWorker_NotFound_Returns404()
    {
        SetupAdminWorker("admin-1");
        _mockRepo.Setup(x => x.GetByIdAsync("nonexistent")).ReturnsAsync((Worker?)null);

        var req = CreateRequest(userId: "admin-1");
        var result = await _functions.DeleteWorker(req, "nonexistent");

        result.Should().BeOfType<NotFoundResult>();
    }
}
