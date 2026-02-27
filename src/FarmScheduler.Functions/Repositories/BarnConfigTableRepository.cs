using Azure.Data.Tables;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class BarnConfigTableRepository : IBarnConfigRepository
{
    private const string TableName = "BarnConfigs";
    private const string PartitionKey = "barnconfig";
    private readonly TableClient _tableClient;

    public BarnConfigTableRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<IReadOnlyList<BarnConfig>> GetAllAsync()
    {
        var configs = new List<BarnConfig>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey))
        {
            configs.Add(MapToConfig(entity));
        }
        return configs;
    }

    public async Task<BarnConfig?> GetAsync(Barn barn)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, barn.ToString());
            return MapToConfig(response.Value);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertAsync(BarnConfig config)
    {
        var entity = new TableEntity(PartitionKey, config.Barn.ToString())
        {
            { "WorkersPerShift", config.WorkersPerShift }
        };
        await _tableClient.UpsertEntityAsync(entity);
    }

    private static BarnConfig MapToConfig(TableEntity entity) => new()
    {
        Barn = Enum.Parse<Barn>(entity.RowKey),
        WorkersPerShift = entity.GetInt32("WorkersPerShift") ?? 1
    };
}
