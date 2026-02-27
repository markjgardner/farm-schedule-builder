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

public class ConfigFunctionsTests
{
    private readonly Mock<IBarnConfigRepository> _mockBarnConfigRepo;
    private readonly Mock<IBlackoutRepository> _mockBlackoutRepo;
    private readonly Mock<IWorkerRepository> _mockWorkerRepo;
    private readonly ConfigFunctions _functions;

    public ConfigFunctionsTests()
    {
        _mockBarnConfigRepo = new Mock<IBarnConfigRepository>();
        _mockBlackoutRepo = new Mock<IBlackoutRepository>();
        _mockWorkerRepo = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<ConfigFunctions>>();

        _functions = new ConfigFunctions(
            _mockBarnConfigRepo.Object,
            _mockBlackoutRepo.Object,
            _mockWorkerRepo.Object,
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

    private static HttpRequest CreateAdminRequestWithBody(object body, string userId = "admin-1")
    {
        var context = new DefaultHttpContext();
        var json = JsonSerializer.Serialize(new { userId, userDetails = "Admin User" });
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        context.Request.Headers["x-ms-client-principal"] = base64;

        var bodyJson = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(bodyJson));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private void SetupAdmin(string userId = "admin-1")
    {
        _mockWorkerRepo.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(new Worker { Id = userId, DisplayName = "Admin", IsActive = true, IsAdmin = true });
    }

    // --- Barn Config Tests ---

    [Fact]
    public async Task GetBarnConfigs_ReturnsAllBarnsWithDefaults()
    {
        SetupAdmin();
        _mockBarnConfigRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<BarnConfig>
        {
            new() { Barn = Barn.York, WorkersPerShift = 2 }
        });

        var result = await _functions.GetBarnConfigs(CreateAdminRequest());

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().Be(200);
        var configs = JsonSerializer.Deserialize<List<BarnConfig>>(content.Content!, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        })!;
        configs.Should().HaveCount(2);
        configs.First(c => c.Barn == Barn.York).WorkersPerShift.Should().Be(2);
        configs.First(c => c.Barn == Barn.Windhover).WorkersPerShift.Should().Be(1);
    }

    [Fact]
    public async Task GetBarnConfigs_Returns401_WhenNoAuth()
    {
        var context = new DefaultHttpContext();
        var result = await _functions.GetBarnConfigs(context.Request);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetBarnConfig_UpdatesConfig()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { workersPerShift = 3 });

        var result = await _functions.SetBarnConfig(req, "York");

        result.Should().BeOfType<ContentResult>();
        _mockBarnConfigRepo.Verify(x => x.UpsertAsync(It.Is<BarnConfig>(c =>
            c.Barn == Barn.York && c.WorkersPerShift == 3)), Times.Once);
    }

    [Fact]
    public async Task SetBarnConfig_RejectsBadBarn()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { workersPerShift = 1 });

        var result = await _functions.SetBarnConfig(req, "InvalidBarn");
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetBarnConfig_RejectsZeroWorkers()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { workersPerShift = 0 });

        var result = await _functions.SetBarnConfig(req, "York");
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- Blackout Tests ---

    [Fact]
    public async Task GetBlackouts_ReturnsAll()
    {
        SetupAdmin();
        var blackouts = new List<BlackoutDate>
        {
            new() { Id = "2024-12-25", Date = new DateOnly(2024, 12, 25), Description = "Christmas" }
        };
        _mockBlackoutRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(blackouts);

        var result = await _functions.GetBlackouts(CreateAdminRequest());

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task AddBlackout_WholeDay()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { date = "2024-12-25", description = "Christmas Day" });

        var result = await _functions.AddBlackout(req);

        result.Should().BeOfType<ContentResult>();
        _mockBlackoutRepo.Verify(x => x.UpsertAsync(It.Is<BlackoutDate>(b =>
            b.Date == new DateOnly(2024, 12, 25) && b.Barn == null && b.Shift == null)), Times.Once);
    }

    [Fact]
    public async Task AddBlackout_BarnSpecific()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { date = "2024-12-25", description = "York closed", barn = "York" });

        var result = await _functions.AddBlackout(req);

        result.Should().BeOfType<ContentResult>();
        _mockBlackoutRepo.Verify(x => x.UpsertAsync(It.Is<BlackoutDate>(b =>
            b.Barn == Barn.York && b.Shift == null)), Times.Once);
    }

    [Fact]
    public async Task AddBlackout_BarnAndShiftSpecific()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new
        {
            date = "2024-12-25",
            description = "No morning at York",
            barn = "York",
            shift = "Morning"
        });

        var result = await _functions.AddBlackout(req);

        result.Should().BeOfType<ContentResult>();
        _mockBlackoutRepo.Verify(x => x.UpsertAsync(It.Is<BlackoutDate>(b =>
            b.Barn == Barn.York && b.Shift == ShiftTime.Morning)), Times.Once);
    }

    [Fact]
    public async Task AddBlackout_RejectsMissingDate()
    {
        SetupAdmin();
        var req = CreateAdminRequestWithBody(new { description = "No date" });

        var result = await _functions.AddBlackout(req);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteBlackout_Succeeds()
    {
        SetupAdmin();
        var result = await _functions.DeleteBlackout(CreateAdminRequest(), "2024-12-25");

        result.Should().BeOfType<OkResult>();
        _mockBlackoutRepo.Verify(x => x.DeleteAsync("2024-12-25"), Times.Once);
    }

    [Fact]
    public async Task DeleteBlackout_Returns401_WhenNoAuth()
    {
        var context = new DefaultHttpContext();
        var result = await _functions.DeleteBlackout(context.Request, "2024-12-25");
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
