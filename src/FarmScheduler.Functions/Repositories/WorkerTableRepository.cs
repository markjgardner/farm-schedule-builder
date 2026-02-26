using Azure.Data.Tables;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class WorkerTableRepository : IWorkerRepository
{
    private const string TableName = "Workers";
    private const string PartitionKey = "worker";
    private readonly TableClient _tableClient;

    public WorkerTableRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<IReadOnlyList<Worker>> GetAllActiveAsync()
    {
        var workers = new List<Worker>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey))
        {
            if (entity.GetBoolean("IsActive") == true)
            {
                workers.Add(MapToWorker(entity));
            }
        }
        return workers;
    }

    public async Task<IReadOnlyList<Worker>> GetAllAsync()
    {
        var workers = new List<Worker>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey))
        {
            workers.Add(MapToWorker(entity));
        }
        return workers;
    }

    public async Task<Worker?> GetByIdAsync(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, workerId);
            return MapToWorker(response.Value);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertAsync(Worker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        var entity = new TableEntity(PartitionKey, worker.Id)
        {
            { "DisplayName", worker.DisplayName },
            { "Email", worker.Email },
            { "IsActive", worker.IsActive },
            { "IsAdmin", worker.IsAdmin }
        };
        await _tableClient.UpsertEntityAsync(entity);
    }

    public async Task DeleteAsync(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        await _tableClient.DeleteEntityAsync(PartitionKey, workerId);
    }

    private static Worker MapToWorker(TableEntity entity) => new()
    {
        Id = entity.RowKey,
        DisplayName = entity.GetString("DisplayName") ?? string.Empty,
        Email = entity.GetString("Email") ?? string.Empty,
        IsActive = entity.GetBoolean("IsActive") ?? true,
        IsAdmin = entity.GetBoolean("IsAdmin") ?? false
    };
}
