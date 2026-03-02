using Microsoft.Azure.Cosmos;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class BarnConfigCosmosRepository : IBarnConfigRepository
{
    private readonly Container _container;

    public BarnConfigCosmosRepository(CosmosClient cosmosClient)
    {
        _container = cosmosClient.GetContainer("FarmScheduler", "barnConfigs");
    }

    public async Task<IReadOnlyList<BarnConfig>> GetAllAsync()
    {
        var configs = new List<BarnConfig>();
        using var feed = _container.GetItemQueryIterator<BarnConfigDocument>(
            new QueryDefinition("SELECT * FROM c"));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            configs.AddRange(response.Select(MapToConfig));
        }
        return configs;
    }

    public async Task<BarnConfig?> GetAsync(Barn barn)
    {
        try
        {
            var id = barn.ToString();
            var response = await _container.ReadItemAsync<BarnConfigDocument>(id, new PartitionKey(id));
            return MapToConfig(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(BarnConfig config)
    {
        var doc = new BarnConfigDocument
        {
            Id = config.Barn.ToString(),
            Barn = config.Barn.ToString(),
            WorkersPerShift = config.WorkersPerShift
        };
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.Barn));
    }

    private static BarnConfig MapToConfig(BarnConfigDocument doc) => new()
    {
        Barn = Enum.Parse<Barn>(doc.Barn),
        WorkersPerShift = doc.WorkersPerShift
    };

    private class BarnConfigDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Barn { get; set; } = string.Empty;
        public int WorkersPerShift { get; set; } = 1;
    }
}
