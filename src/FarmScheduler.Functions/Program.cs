using FarmScheduler.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddFarmSchedulerServices(context.Configuration);
    })
    .ConfigureLogging(logging =>
    {
        logging.AddApplicationInsights();
    })
    .Build();

host.Run();
