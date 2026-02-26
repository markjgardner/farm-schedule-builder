using System.Text.Json;
using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class AdminFunctions
{
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<AdminFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminFunctions(IWorkerRepository workerRepository, ILogger<AdminFunctions> logger)
    {
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

    [Function("AdminGetWorkers")]
    public async Task<IActionResult> GetWorkers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage/workers")] HttpRequest req)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var workers = await _workerRepository.GetAllAsync();
        return new OkObjectResult(workers);
    }

    [Function("AdminCreateWorker")]
    public async Task<IActionResult> CreateWorker(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/workers")] HttpRequest req)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions);
        var displayName = body.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
        var email = body.TryGetProperty("email", out var em) ? em.GetString() : null;

        if (string.IsNullOrEmpty(displayName))
            return new BadRequestObjectResult("displayName is required.");

        var worker = new Worker
        {
            Id = $"manual_{Guid.NewGuid():N}",
            DisplayName = displayName,
            Email = email ?? string.Empty,
            IsActive = true
        };

        await _workerRepository.UpsertAsync(worker);
        _logger.LogInformation("Admin created worker {WorkerId} ({DisplayName})", worker.Id, worker.DisplayName);

        return new OkObjectResult(worker);
    }

    [Function("AdminDeactivateWorker")]
    public async Task<IActionResult> DeactivateWorker(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/workers/{id}/deactivate")] HttpRequest req,
        string id)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var worker = await _workerRepository.GetByIdAsync(id);
        if (worker == null)
            return new NotFoundResult();

        worker.IsActive = false;
        await _workerRepository.UpsertAsync(worker);
        _logger.LogInformation("Admin deactivated worker {WorkerId}", id);

        return new OkObjectResult(worker);
    }

    [Function("AdminActivateWorker")]
    public async Task<IActionResult> ActivateWorker(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/workers/{id}/activate")] HttpRequest req,
        string id)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var worker = await _workerRepository.GetByIdAsync(id);
        if (worker == null)
            return new NotFoundResult();

        worker.IsActive = true;
        await _workerRepository.UpsertAsync(worker);
        _logger.LogInformation("Admin activated worker {WorkerId}", id);

        return new OkObjectResult(worker);
    }

    [Function("AdminToggleAdmin")]
    public async Task<IActionResult> ToggleAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage/workers/{id}/admin")] HttpRequest req,
        string id)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions);
        var isAdmin = body.TryGetProperty("isAdmin", out var ia) && ia.GetBoolean();

        var worker = await _workerRepository.GetByIdAsync(id);
        if (worker == null)
            return new NotFoundResult();

        worker.IsAdmin = isAdmin;
        await _workerRepository.UpsertAsync(worker);
        _logger.LogInformation("Admin set IsAdmin={IsAdmin} for worker {WorkerId}", isAdmin, id);

        return new OkObjectResult(worker);
    }

    [Function("AdminDeleteWorker")]
    public async Task<IActionResult> DeleteWorker(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "manage/workers/{id}")] HttpRequest req,
        string id)
    {
        var (_, error) = await RequireAdminAsync(req);
        if (error != null) return error;

        var worker = await _workerRepository.GetByIdAsync(id);
        if (worker == null)
            return new NotFoundResult();

        await _workerRepository.DeleteAsync(id);
        _logger.LogInformation("Admin deleted worker {WorkerId}", id);

        return new OkResult();
    }
}
