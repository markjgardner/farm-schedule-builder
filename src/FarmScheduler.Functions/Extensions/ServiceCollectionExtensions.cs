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
            // In production the Bicep template sets StorageTableEndpoint to the full
            // table service URI (e.g. https://<account>.table.core.windows.net/).
            var storageTableEndpoint = configuration["StorageTableEndpoint"];
            if (!string.IsNullOrEmpty(storageTableEndpoint))
            {
                services.AddSingleton(new TableServiceClient(
                    new Uri(storageTableEndpoint),
                    new DefaultAzureCredential()));
            }
            else
            {
                var storageAccountName = configuration["StorageAccountName"];
                services.AddSingleton(new TableServiceClient(
                    new Uri($"https://{storageAccountName}.table.core.windows.net"),
                    new DefaultAzureCredential()));
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
            // In production the Bicep template sets ServiceBus__fullyQualifiedNamespace
            // which .NET configuration resolves as ServiceBus:fullyQualifiedNamespace.
            var serviceBusNamespace = configuration["ServiceBus:fullyQualifiedNamespace"]
                ?? configuration["ServiceBusNamespace"];
            if (!string.IsNullOrEmpty(serviceBusNamespace))
            {
                // The value may already be a FQDN (host.servicebus.windows.net) or just a name.
                var fqdn = serviceBusNamespace.Contains('.')
                    ? serviceBusNamespace
                    : $"{serviceBusNamespace}.servicebus.windows.net";
                services.AddSingleton(new ServiceBusClient(fqdn, new DefaultAzureCredential()));
            }
        }

        // Repositories
        services.AddSingleton<IWorkerRepository, WorkerTableRepository>();
        services.AddSingleton<IAvailabilityRepository, AvailabilityTableRepository>();
        services.AddSingleton<IBarnConfigRepository, BarnConfigTableRepository>();
        services.AddSingleton<IBlackoutRepository, BlackoutTableRepository>();

        // Services
        services.AddSingleton<ISchedulingService, SchedulingService>();
        services.AddSingleton<IAvailabilityService, AvailabilityService>();

        return services;
    }
}
