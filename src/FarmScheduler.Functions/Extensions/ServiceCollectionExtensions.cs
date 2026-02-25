using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmScheduler.Functions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFarmSchedulerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Table Storage
        var storageConnection = configuration["StorageConnectionString"];
        if (storageConnection == "UseDevelopmentStorage=true" || !string.IsNullOrEmpty(storageConnection))
        {
            services.AddSingleton(new TableServiceClient(storageConnection));
        }
        else
        {
            var storageAccountName = configuration["StorageAccountName"];
            services.AddSingleton(new TableServiceClient(
                new Uri($"https://{storageAccountName}.table.core.windows.net"),
                new DefaultAzureCredential()));
        }

        // Service Bus
        var serviceBusConnection = configuration["ServiceBusConnectionString"];
        if (!string.IsNullOrEmpty(serviceBusConnection))
        {
            services.AddSingleton(new ServiceBusClient(serviceBusConnection));
        }
        else
        {
            var serviceBusNamespace = configuration["ServiceBusNamespace"];
            if (!string.IsNullOrEmpty(serviceBusNamespace))
            {
                services.AddSingleton(new ServiceBusClient(
                    $"{serviceBusNamespace}.servicebus.windows.net",
                    new DefaultAzureCredential()));
            }
        }

        // Repositories
        services.AddSingleton<IWorkerRepository, WorkerTableRepository>();
        services.AddSingleton<IAvailabilityRepository, AvailabilityTableRepository>();

        // Services
        services.AddSingleton<ISchedulingService, SchedulingService>();
        services.AddSingleton<IAvailabilityService, AvailabilityService>();

        return services;
    }
}
