using System.Text.Json;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class AvailabilityFunctions
{
    private readonly IAvailabilityService _availabilityService;
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<AvailabilityFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AvailabilityFunctions(IAvailabilityService availabilityService, IWorkerRepository workerRepository, ILogger<AvailabilityFunctions> logger)
    {
        _availabilityService = availabilityService;
        _workerRepository = workerRepository;
        _logger = logger;
    }

    private async Task<(Worker? admin, IActionResult? error)> RequireAdminAsync(HttpRequest req)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return (null, new UnauthorizedResult());

        var worker = await _workerRepository.GetByIdAsync(principal.UserId);
        if (worker == null || !worker.IsAdmin)
            return (null, new ObjectResult("Forbidden") { StatusCode = 403 });

        return (worker, null);
    }

    [Function("GetAvailability")]
    public async Task<IActionResult> GetAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "availability/{windowStart}")] HttpRequest req,
        string windowStart)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return new UnauthorizedResult();

        var availability = await _availabilityService.GetAvailabilityAsync(windowStart, principal.UserId);
        return new OkObjectResult(availability);
    }

    [Function("PutAvailability")]
    public async Task<IActionResult> PutAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "availability/{windowStart}")] HttpRequest req,
        string windowStart)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return new UnauthorizedResult();

        var worker = await _workerRepository.GetByIdAsync(principal.UserId);
        if (worker == null || !worker.IsActive)
            return new ObjectResult("Inactive workers cannot set availability.") { StatusCode = 403 };

        var items = await JsonSerializer.DeserializeAsync<List<Availability>>(req.Body, JsonOptions);
        if (items == null || items.Count == 0)
            return new BadRequestObjectResult("Request body must be a non-empty array of availability entries.");

        await _availabilityService.SetAvailabilityAsync(windowStart, principal.UserId, items);
        _logger.LogInformation("Saved {Count} availability entries for worker {WorkerId} window {Window}",
            items.Count, principal.UserId, windowStart);

        return new OkObjectResult(new { saved = items.Count });
    }

    [Function("AdminGetAvailability")]
    public async Task<IActionResult> AdminGetAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/availability/{windowStart}/{workerId}")] HttpRequest req,
        string windowStart,
        string workerId)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var availability = await _availabilityService.GetAvailabilityAsync(windowStart, workerId);
        return new OkObjectResult(availability);
    }

    [Function("AdminPutAvailability")]
    public async Task<IActionResult> AdminPutAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/availability/{windowStart}/{workerId}")] HttpRequest req,
        string windowStart,
        string workerId)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var items = await JsonSerializer.DeserializeAsync<List<Availability>>(req.Body, JsonOptions);
        if (items == null || items.Count == 0)
            return new BadRequestObjectResult("Request body must be a non-empty array of availability entries.");

        await _availabilityService.SetAvailabilityAsync(windowStart, workerId, items);
        _logger.LogInformation("Admin saved {Count} availability entries for worker {WorkerId} window {Window}",
            items.Count, workerId, windowStart);

        return new OkObjectResult(new { saved = items.Count });
    }
}
