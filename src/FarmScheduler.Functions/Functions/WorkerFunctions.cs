using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class WorkerFunctions
{
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<WorkerFunctions> _logger;

    public WorkerFunctions(IWorkerRepository workerRepository, ILogger<WorkerFunctions> logger)
    {
        _workerRepository = workerRepository;
        _logger = logger;
    }

    [Function("GetWorkers")]
    public async Task<IActionResult> GetWorkers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workers")] HttpRequest req)
    {
        var workers = await _workerRepository.GetAllActiveAsync();
        return new OkObjectResult(workers);
    }

    [Function("RegisterWorker")]
    public async Task<IActionResult> RegisterWorker(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workers")] HttpRequest req)
    {
        var (userId, userDetails) = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(userId))
            return new UnauthorizedResult();

        var worker = new Worker
        {
            Id = userId,
            DisplayName = userDetails ?? userId,
            IsActive = true
        };

        await _workerRepository.UpsertAsync(worker);
        _logger.LogInformation("Registered/updated worker {WorkerId} ({DisplayName})", worker.Id, worker.DisplayName);

        return new OkObjectResult(worker);
    }
}
