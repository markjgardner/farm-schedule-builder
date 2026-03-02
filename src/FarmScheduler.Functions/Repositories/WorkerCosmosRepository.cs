using Microsoft.Azure.Cosmos;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class WorkerCosmosRepository : IWorkerRepository
{
    private readonly Container _container;

    public WorkerCosmosRepository(CosmosClient cosmosClient)
    {
        _container = cosmosClient.GetContainer("FarmScheduler", "workers");
    }

    public async Task<IReadOnlyList<Worker>> GetAllActiveAsync()
    {
        var workers = new List<Worker>();
        using var feed = _container.GetItemQueryIterator<WorkerDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.isActive = true"));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            workers.AddRange(response.Select(MapToWorker));
        }
        return workers;
    }

    public async Task<IReadOnlyList<Worker>> GetAllAsync()
    {
        var workers = new List<Worker>();
        using var feed = _container.GetItemQueryIterator<WorkerDocument>(
            new QueryDefinition("SELECT * FROM c"));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            workers.AddRange(response.Select(MapToWorker));
        }
        return workers;
    }

    public async Task<Worker?> GetByIdAsync(string workerId)
    {
        try
        {
            var response = await _container.ReadItemAsync<WorkerDocument>(workerId, new PartitionKey(workerId));
            return MapToWorker(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(Worker worker)
    {
        var doc = new WorkerDocument
        {
            Id = worker.Id,
            DisplayName = worker.DisplayName,
            Email = worker.Email,
            IsActive = worker.IsActive,
            IsAdmin = worker.IsAdmin
        };
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.Id));
    }

    public async Task DeleteAsync(string workerId)
    {
        try
        {
            await _container.DeleteItemAsync<WorkerDocument>(workerId, new PartitionKey(workerId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted
        }
    }

    private static Worker MapToWorker(WorkerDocument doc) => new()
    {
        Id = doc.Id,
        DisplayName = doc.DisplayName,
        Email = doc.Email,
        IsActive = doc.IsActive,
        IsAdmin = doc.IsAdmin
    };

    private class WorkerDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }
    }
}
