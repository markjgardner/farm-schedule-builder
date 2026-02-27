using System.Text.Json;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class ConfigFunctions
{
    private readonly IBarnConfigRepository _barnConfigRepo;
    private readonly IBlackoutRepository _blackoutRepo;
    private readonly IWorkerRepository _workerRepo;
    private readonly ILogger<ConfigFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ConfigFunctions(
        IBarnConfigRepository barnConfigRepo,
        IBlackoutRepository blackoutRepo,
        IWorkerRepository workerRepo,
        ILogger<ConfigFunctions> logger)
    {
        _barnConfigRepo = barnConfigRepo;
        _blackoutRepo = blackoutRepo;
        _workerRepo = workerRepo;
        _logger = logger;
    }

    private async Task<(Worker? admin, IActionResult? error)> RequireAdminAsync(HttpRequest req)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return (null, new UnauthorizedResult());

        var worker = await _workerRepo.GetByIdAsync(principal.UserId);
        if (worker == null || !worker.IsAdmin)
            return (null, new ObjectResult("Forbidden") { StatusCode = 403 });

        return (worker, null);
    }

    // --- Barn Configuration ---

    [Function("ConfigGetBarns")]
    public async Task<IActionResult> GetBarnConfigs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/config/barns")] HttpRequest req)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var configs = await _barnConfigRepo.GetAllAsync();

        // Return all barns, filling in defaults for unconfigured ones
        var configLookup = configs.ToDictionary(c => c.Barn);
        var result = Enum.GetValues<Barn>()
            .Select(b => configLookup.TryGetValue(b, out var c) ? c : new BarnConfig { Barn = b, WorkersPerShift = 1 })
            .ToList();

        return new ContentResult
        {
            Content = JsonSerializer.Serialize(result, JsonOptions),
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("ConfigSetBarn")]
    public async Task<IActionResult> SetBarnConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/config/barns/{barn}")] HttpRequest req,
        string barn)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        if (!Enum.TryParse<Barn>(barn, true, out var barnEnum))
            return new BadRequestObjectResult($"Invalid barn: {barn}");

        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions);
        var workersPerShift = body.TryGetProperty("workersPerShift", out var wps) ? wps.GetInt32() : 1;

        if (workersPerShift < 1)
            return new BadRequestObjectResult("workersPerShift must be at least 1");

        var config = new BarnConfig { Barn = barnEnum, WorkersPerShift = workersPerShift };
        await _barnConfigRepo.UpsertAsync(config);

        _logger.LogInformation("Admin set barn {Barn} to {Workers} workers per shift", barn, workersPerShift);
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(config, JsonOptions),
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    // --- Blackout Dates ---

    [Function("ConfigGetBlackouts")]
    public async Task<IActionResult> GetBlackouts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/config/blackouts")] HttpRequest req)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var blackouts = await _blackoutRepo.GetAllAsync();
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(blackouts, JsonOptions),
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("ConfigAddBlackout")]
    public async Task<IActionResult> AddBlackout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/config/blackouts")] HttpRequest req)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions);

        if (!body.TryGetProperty("date", out var dateProp) || !DateOnly.TryParse(dateProp.GetString(), out var date))
            return new BadRequestObjectResult("date is required (yyyy-MM-dd format)");

        var description = body.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";

        Barn? barn = null;
        if (body.TryGetProperty("barn", out var barnProp) && barnProp.ValueKind != JsonValueKind.Null)
        {
            if (Enum.TryParse<Barn>(barnProp.GetString(), true, out var b))
                barn = b;
        }

        ShiftTime? shift = null;
        if (body.TryGetProperty("shift", out var shiftProp) && shiftProp.ValueKind != JsonValueKind.Null)
        {
            if (Enum.TryParse<ShiftTime>(shiftProp.GetString(), true, out var s))
                shift = s;
        }

        var blackout = new BlackoutDate
        {
            Date = date,
            Description = description,
            Barn = barn,
            Shift = shift
        };

        await _blackoutRepo.UpsertAsync(blackout);
        _logger.LogInformation("Admin added blackout for {Date} (barn={Barn}, shift={Shift})",
            date, barn?.ToString() ?? "all", shift?.ToString() ?? "all");

        return new ContentResult
        {
            Content = JsonSerializer.Serialize(blackout, JsonOptions),
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("ConfigDeleteBlackout")]
    public async Task<IActionResult> DeleteBlackout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "manage/config/blackouts/{id}")] HttpRequest req,
        string id)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        await _blackoutRepo.DeleteAsync(id);
        _logger.LogInformation("Admin deleted blackout {Id}", id);

        return new OkResult();
    }
}
