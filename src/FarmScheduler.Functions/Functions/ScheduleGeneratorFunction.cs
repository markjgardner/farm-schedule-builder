using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FarmScheduler.Functions.Functions;

public class ScheduleGeneratorFunction
{
    private readonly IWorkerRepository _workerRepository;
    private readonly IAvailabilityService _availabilityService;
    private readonly ISchedulingService _schedulingService;
    private readonly IBarnConfigRepository _barnConfigRepository;
    private readonly IBlackoutRepository _blackoutRepository;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ScheduleGeneratorFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ScheduleGeneratorFunction(
        IWorkerRepository workerRepository,
        IAvailabilityService availabilityService,
        ISchedulingService schedulingService,
        IBarnConfigRepository barnConfigRepository,
        IBlackoutRepository blackoutRepository,
        ServiceBusClient serviceBusClient,
        ILogger<ScheduleGeneratorFunction> logger)
    {
        _workerRepository = workerRepository;
        _availabilityService = availabilityService;
        _schedulingService = schedulingService;
        _barnConfigRepository = barnConfigRepository;
        _blackoutRepository = blackoutRepository;
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    [Function("ScheduleGeneratorTimer")]
    public async Task RunTimer(
        [TimerTrigger("0 0 12 */14 * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Schedule generator timer triggered at {Time}", DateTime.UtcNow);
        await GenerateAndPublishScheduleAsync();
    }

    [Function("ScheduleGeneratorHttp")]
    public async Task<IActionResult> RunHttp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/schedule/generate")] HttpRequest req)
    {
        var principal = AuthHelper.ParseClientPrincipal(req);
        if (string.IsNullOrEmpty(principal.UserId))
            return new UnauthorizedResult();

        var worker = await _workerRepository.GetByIdAsync(principal.UserId);
        if (worker == null || !worker.IsAdmin)
            return new ObjectResult("Forbidden") { StatusCode = 403 };

        _logger.LogInformation("Schedule generation manually triggered by admin {AdminId}", principal.UserId);
        var schedule = await GenerateAndPublishScheduleAsync();
        return new OkObjectResult(schedule);
    }

    internal async Task<object> GenerateAndPublishScheduleAsync()
    {
        // Compute next 2-week scheduling window (next Monday through Sunday +2 weeks)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var windowStart = today.AddDays(daysUntilMonday);
        var windowEnd = windowStart.AddDays(13); // 2 weeks = 14 days, inclusive

        var windowStartStr = windowStart.ToString("yyyy-MM-dd");

        _logger.LogInformation("Generating schedule for window {Start} to {End}", windowStart, windowEnd);

        var workers = await _workerRepository.GetAllActiveAsync();
        var availability = await _availabilityService.GetAvailabilityAsync(windowStartStr);
        var barnConfigs = await _barnConfigRepository.GetAllAsync();
        var blackouts = await _blackoutRepository.GetForWindowAsync(windowStart, windowEnd);
        var schedule = _schedulingService.GenerateSchedule(workers, availability, windowStart, windowEnd, barnConfigs, blackouts);

        var json = JsonSerializer.Serialize(schedule, JsonOptions);

        await using var sender = _serviceBusClient.CreateSender("schedule-generated");
        await sender.SendMessageAsync(new ServiceBusMessage(json));

        _logger.LogInformation(
            "Schedule published: {Assignments} assignments for {Start} to {End}",
            schedule.Assignments.Count, windowStart, windowEnd);

        return schedule;
    }
}
