using Microsoft.Azure.Cosmos;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class AvailabilityCosmosRepository : IAvailabilityRepository
{
    private readonly Container _container;

    public AvailabilityCosmosRepository(CosmosClient cosmosClient)
    {
        _container = cosmosClient.GetContainer("FarmScheduler", "availability");
    }

    public async Task<IReadOnlyList<Availability>> GetByWindowAsync(string windowStart)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        var items = new List<Availability>();
        using var feed = _container.GetItemQueryIterator<AvailabilityDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.windowStart = @ws")
                .WithParameter("@ws", windowStart));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            items.AddRange(response.Select(MapToAvailability));
        }
        return items;
    }

    public async Task<IReadOnlyList<Availability>> GetByWindowAndWorkerAsync(string windowStart, string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        var items = new List<Availability>();
        using var feed = _container.GetItemQueryIterator<AvailabilityDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.windowStart = @ws AND c.workerId = @wid")
                .WithParameter("@ws", windowStart)
                .WithParameter("@wid", workerId));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            items.AddRange(response.Select(MapToAvailability));
        }
        return items;
    }

    public async Task UpsertAsync(string windowStart, Availability availability)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentNullException.ThrowIfNull(availability);
        var doc = MapToDocument(windowStart, availability);
        await _container.UpsertItemAsync(doc, new PartitionKey(windowStart));
    }

    public async Task UpsertBatchAsync(string windowStart, IReadOnlyList<Availability> availability)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentNullException.ThrowIfNull(availability);
        if (availability.Count == 0) return;

        var batch = _container.CreateTransactionalBatch(new PartitionKey(windowStart));
        foreach (var item in availability)
        {
            batch.UpsertItem(MapToDocument(windowStart, item));
        }
        using var response = await batch.ExecuteAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Batch upsert failed with status {response.StatusCode}");
        }
    }

    private static AvailabilityDocument MapToDocument(string windowStart, Availability a) => new()
    {
        Id = $"{a.WorkerId}_{a.Date:yyyy-MM-dd}",
        WindowStart = windowStart,
        WorkerId = a.WorkerId,
        Date = a.Date.ToString("yyyy-MM-dd"),
        Status = a.Status.ToString()
    };

    private static Availability MapToAvailability(AvailabilityDocument doc) => new()
    {
        WorkerId = doc.WorkerId,
        Date = DateOnly.Parse(doc.Date),
        Status = Enum.Parse<AvailabilityStatus>(doc.Status)
    };

    private class AvailabilityDocument
    {
        public string Id { get; set; } = string.Empty;
        public string WindowStart { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
