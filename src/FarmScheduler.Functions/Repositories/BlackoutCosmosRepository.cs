using Microsoft.Azure.Cosmos;
using FarmScheduler.Core.Models;

namespace FarmScheduler.Functions.Repositories;

public class BlackoutCosmosRepository : IBlackoutRepository
{
    private readonly Container _container;

    public BlackoutCosmosRepository(CosmosClient cosmosClient)
    {
        _container = cosmosClient.GetContainer("FarmScheduler", "blackouts");
    }

    public async Task<IReadOnlyList<BlackoutDate>> GetAllAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var blackouts = new List<BlackoutDate>();
        using var feed = _container.GetItemQueryIterator<BlackoutDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.date >= @today")
                .WithParameter("@today", today));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            blackouts.AddRange(response.Select(MapToBlackout));
        }
        return blackouts;
    }

    public async Task<IReadOnlyList<BlackoutDate>> GetForWindowAsync(DateOnly start, DateOnly end)
    {
        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");
        var blackouts = new List<BlackoutDate>();
        using var feed = _container.GetItemQueryIterator<BlackoutDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.date >= @start AND c.date <= @end")
                .WithParameter("@start", startStr)
                .WithParameter("@end", endStr));
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            blackouts.AddRange(response.Select(MapToBlackout));
        }
        return blackouts;
    }

    public async Task UpsertAsync(BlackoutDate blackout)
    {
        var id = GenerateId(blackout);
        blackout.Id = id;

        var doc = new BlackoutDocument
        {
            Id = id,
            Date = blackout.Date.ToString("yyyy-MM-dd"),
            Description = blackout.Description,
            Barn = blackout.Barn?.ToString() ?? string.Empty,
            Shift = blackout.Shift?.ToString() ?? string.Empty,
            Ttl = ComputeTtl(blackout.Date)
        };
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.Id));
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<BlackoutDocument>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted or expired
        }
    }

    /// <summary>Compute TTL in seconds: expire at end of the blackout date (midnight UTC next day).</summary>
    private static int ComputeTtl(DateOnly blackoutDate)
    {
        var expiresAt = blackoutDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var seconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;
        return Math.Max(1, seconds); // Minimum 1 second TTL
    }

    private static string GenerateId(BlackoutDate blackout)
    {
        var parts = blackout.Date.ToString("yyyy-MM-dd");
        if (blackout.Barn.HasValue)
            parts += $"_{blackout.Barn.Value}";
        if (blackout.Shift.HasValue)
            parts += $"_{blackout.Shift.Value}";
        return parts;
    }

    private static BlackoutDate MapToBlackout(BlackoutDocument doc) => new()
    {
        Id = doc.Id,
        Date = DateOnly.ParseExact(doc.Date, "yyyy-MM-dd"),
        Description = doc.Description,
        Barn = string.IsNullOrEmpty(doc.Barn) ? null : Enum.Parse<Barn>(doc.Barn),
        Shift = string.IsNullOrEmpty(doc.Shift) ? null : Enum.Parse<ShiftTime>(doc.Shift)
    };

    private class BlackoutDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Barn { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public int Ttl { get; set; }
    }
}
