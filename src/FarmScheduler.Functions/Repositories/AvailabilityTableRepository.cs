using Azure.Data.Tables;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class AvailabilityTableRepository : IAvailabilityRepository
{
    private const string TableName = "Availability";
    private readonly TableClient _tableClient;

    public AvailabilityTableRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<IReadOnlyList<Availability>> GetByWindowAsync(string windowStart)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        var items = new List<Availability>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == windowStart))
        {
            items.Add(MapToAvailability(entity));
        }
        return items;
    }

    public async Task<IReadOnlyList<Availability>> GetByWindowAndWorkerAsync(string windowStart, string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        var items = new List<Availability>();
        var prefix = $"{workerId}_";
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            e => e.PartitionKey == windowStart && e.RowKey.CompareTo(prefix) >= 0 && e.RowKey.CompareTo(prefix + "~") < 0))
        {
            items.Add(MapToAvailability(entity));
        }
        return items;
    }

    public async Task UpsertAsync(string windowStart, Availability availability)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentNullException.ThrowIfNull(availability);
        var entity = MapToEntity(windowStart, availability);
        await _tableClient.UpsertEntityAsync(entity);
    }

    public async Task UpsertBatchAsync(string windowStart, IReadOnlyList<Availability> availability)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowStart);
        ArgumentNullException.ThrowIfNull(availability);

        if (availability.Count == 0) return;

        var batch = new List<TableTransactionAction>();
        foreach (var item in availability)
        {
            batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, MapToEntity(windowStart, item)));
        }

        // Table Storage batch operations require all entities in the same partition
        // and max 100 per batch
        foreach (var chunk in batch.Chunk(100))
        {
            await _tableClient.SubmitTransactionAsync(chunk);
        }
    }

    private static TableEntity MapToEntity(string windowStart, Availability availability) => new(windowStart, $"{availability.WorkerId}_{availability.Date:yyyy-MM-dd}")
    {
        { "WorkerId", availability.WorkerId },
        { "Date", availability.Date.ToString("yyyy-MM-dd") },
        { "Status", availability.Status.ToString() }
    };

    private static Availability MapToAvailability(TableEntity entity) => new()
    {
        WorkerId = entity.GetString("WorkerId") ?? string.Empty,
        Date = DateOnly.Parse(entity.GetString("Date") ?? "2000-01-01"),
        Status = Enum.Parse<AvailabilityStatus>(entity.GetString("Status") ?? "Available")
    };
}
