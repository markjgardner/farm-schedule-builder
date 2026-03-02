using FarmScheduler.Core.Models;
using FarmScheduler.Functions.Repositories;
using FarmScheduler.Functions.Services;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmScheduler.Functions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFarmSchedulerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Cosmos DB
        var cosmosConnection = configuration["CosmosDbConnectionString"];
        if (!string.IsNullOrEmpty(cosmosConnection))
        {
            services.AddSingleton(new CosmosClient(cosmosConnection));
        }
        else
        {
            var cosmosEndpoint = configuration["CosmosDbEndpoint"];
            if (!string.IsNullOrEmpty(cosmosEndpoint))
            {
                services.AddSingleton(new CosmosClient(cosmosEndpoint, new DefaultAzureCredential()));
            }
        }

        // Service Bus
        var serviceBusConnection = configuration["ServiceBusConnectionString"];
        if (!string.IsNullOrEmpty(serviceBusConnection))
        {
            services.AddSingleton(new ServiceBusClient(serviceBusConnection));
        }
        else
        {
            var serviceBusNamespace = configuration["ServiceBus:fullyQualifiedNamespace"]
                ?? configuration["ServiceBusNamespace"];
            if (!string.IsNullOrEmpty(serviceBusNamespace))
            {
                var fqdn = serviceBusNamespace.Contains('.')
                    ? serviceBusNamespace
                    : $"{serviceBusNamespace}.servicebus.windows.net";
                services.AddSingleton(new ServiceBusClient(fqdn, new DefaultAzureCredential()));
            }
        }

        // Repositories
        services.AddSingleton<IWorkerRepository, WorkerCosmosRepository>();
        services.AddSingleton<IAvailabilityRepository, AvailabilityCosmosRepository>();
        services.AddSingleton<IBarnConfigRepository, BarnConfigCosmosRepository>();
        services.AddSingleton<IBlackoutRepository, BlackoutCosmosRepository>();

        // Services
        services.AddSingleton<ISchedulingService, SchedulingService>();
        services.AddSingleton<IAvailabilityService, AvailabilityService>();

        return services;
    }
}
