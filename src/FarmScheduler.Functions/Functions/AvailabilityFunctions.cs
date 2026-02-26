using System.Text.Json;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class AvailabilityFunctions
{
    private readonly IAvailabilityService _availabilityService;
    private readonly ILogger<AvailabilityFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AvailabilityFunctions(IAvailabilityService availabilityService, ILogger<AvailabilityFunctions> logger)
    {
        _availabilityService = availabilityService;
        _logger = logger;
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

        var items = await JsonSerializer.DeserializeAsync<List<Availability>>(req.Body, JsonOptions);
        if (items == null || items.Count == 0)
            return new BadRequestObjectResult("Request body must be a non-empty array of availability entries.");

        await _availabilityService.SetAvailabilityAsync(windowStart, principal.UserId, items);
        _logger.LogInformation("Saved {Count} availability entries for worker {WorkerId} window {Window}",
            items.Count, principal.UserId, windowStart);

        return new OkObjectResult(new { saved = items.Count });
    }

    [Function("GetAllAvailability")]
    public async Task<IActionResult> GetAllAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "availability/{windowStart}/all")] HttpRequest req,
        string windowStart)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return new UnauthorizedResult();

        var availability = await _availabilityService.GetAvailabilityAsync(windowStart);
        return new OkObjectResult(availability);
    }
}
