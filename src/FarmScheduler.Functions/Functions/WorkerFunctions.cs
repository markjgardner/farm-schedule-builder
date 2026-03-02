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

    [Function("GetMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return new UnauthorizedResult();

        var worker = await _workerRepository.GetByIdAsync(principal.UserId);
        if (worker == null || (!worker.IsActive && !worker.IsAdmin))
        {
            _logger.LogWarning("Unregistered user attempted access: {UserId} ({UserDetails})",
                principal.UserId, principal.UserDetails);
            return new ObjectResult(new { error = "User is not a registered worker. Contact an administrator." })
                { StatusCode = 403 };
        }

        return new OkObjectResult(worker);
    }
}
